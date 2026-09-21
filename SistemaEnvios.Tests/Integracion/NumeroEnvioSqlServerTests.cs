using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// El número de envío, contra el motor que lo calcula.
///
/// No se puede comprobar en memoria: la columna es calculada y el proveedor InMemory no sabe
/// calcular nada, así que la declaración vive solo para SQL Server. Esto es lo único que cubre
/// el formato.
///
/// Escribe, así que monta su propia base desechable.
/// </summary>
public sealed class NumeroEnvioSqlServerTests : IAsyncLifetime
{
    private static string? CadenaBase => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    private readonly string _baseDesechable = $"ADRTrack_Numero_{Guid.NewGuid():N}";
    private string _cadena = string.Empty;
    private int _filialId;
    private int _tecnologiaId;
    private int _estadoId;

    /// <summary>
    /// Las fronteras que hay que cuidar: son las 8 PM locales del último día del mes, y ese día
    /// cambia según el mes. Se dan en UTC porque así las guarda la base.
    /// </summary>
    public static TheoryData<string, string, string> Fronteras => new()
    {
        { "2027-02-01T03:59:00", "2027-01", "31 de enero, 11:59 PM: mes de 31 días" },
        { "2027-03-01T01:00:00", "2027-02", "28 de febrero, 9 PM: mes de 28 días" },
        { "2028-03-01T01:00:00", "2028-02", "29 de febrero de 2028: año bisiesto" },
        { "2027-05-01T00:30:00", "2027-04", "30 de abril, 8:30 PM: mes de 30 días" },
        { "2027-01-01T03:00:00", "2026-12", "31 de diciembre, 11 PM: cambia el año" },
        { "2027-09-15T12:00:00", "2027-09", "mediodía de un día cualquiera" },
    };

    [SkippableTheory]
    [MemberData(nameof(Fronteras))]
    public async Task ElMesSaleDeLaHoraLocalYNoDeLaUtc(string fechaUtc, string esperado, string caso)
    {
        Saltar();
        await using var db = Contexto();

        var envio = await CrearAsync(db, DateTime.Parse(fechaUtc, System.Globalization.CultureInfo.InvariantCulture));

        Assert.StartsWith($"ENV-{esperado}-", envio.NumeroEnvio);
        Assert.True(envio.NumeroEnvio.Length == 18, $"{caso}: {envio.NumeroEnvio}");
    }

    /// <summary>La cola es el EnvioId, así que el número sale solo y sin contador aparte.</summary>
    [SkippableFact]
    public async Task LaColaEsElIdDelEnvio()
    {
        Saltar();
        await using var db = Contexto();

        var envio = await CrearAsync(db, new DateTime(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc));

        Assert.EndsWith(envio.EnvioId.ToString("D6"), envio.NumeroEnvio);
    }

    /// <summary>
    /// El API no escribe el número: lo pone la base. Si alguien lo asignara, EF lo ignora y la
    /// base manda, que es justo lo que se quiere.
    /// </summary>
    [SkippableFact]
    public async Task LoQueAsigneElCodigoSeIgnora()
    {
        Saltar();
        await using var db = Contexto();

        var envio = await CrearAsync(db, new DateTime(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc),
            numeroInventado: "ENV-INVENTADO-999");

        Assert.NotEqual("ENV-INVENTADO-999", envio.NumeroEnvio);
        Assert.StartsWith("ENV-2026-09-", envio.NumeroEnvio);
    }

    /// <summary>Sin fecha, la pone SQL Server: es el único reloj que decide el número.</summary>
    [SkippableFact]
    public async Task SinFechaLaSellaLaBase()
    {
        Saltar();
        await using var db = Contexto();

        var envio = await CrearAsync(db, fecha: null);

        Assert.NotEqual(default, envio.FechaCreacion);
        var esperado = DateTime.UtcNow.AddHours(-4).ToString("yyyy-MM");
        Assert.StartsWith($"ENV-{esperado}-", envio.NumeroEnvio);
    }

    /// <summary>Ordenar por número es ordenar por fecha: el relleno con ceros lo garantiza.</summary>
    [SkippableFact]
    public async Task OrdenarPorNumeroEsOrdenarPorFecha()
    {
        Saltar();
        await using var db = Contexto();

        await CrearAsync(db, new DateTime(2026, 9, 10, 15, 0, 0, DateTimeKind.Utc));
        await CrearAsync(db, new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc));
        await CrearAsync(db, new DateTime(2027, 1, 10, 15, 0, 0, DateTimeKind.Utc));

        var porNumero = await db.Envios.AsNoTracking().OrderBy(x => x.NumeroEnvio)
            .Select(x => x.EnvioId).ToListAsync();
        var porFecha = await db.Envios.AsNoTracking().OrderBy(x => x.FechaCreacion)
            .Select(x => x.EnvioId).ToListAsync();

        Assert.Equal(porFecha, porNumero);
    }

    private async Task<Envio> CrearAsync(
        SistemaEnviosDbContext db, DateTime? fecha, string? numeroInventado = null)
    {
        var envio = new Envio
        {
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            EstadoEnvioId = _estadoId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = Guid.NewGuid(),
        };
        if (fecha.HasValue) envio.FechaCreacion = fecha.Value;
        if (numeroInventado is not null) envio.NumeroEnvio = numeroInventado;

        db.Envios.Add(envio);
        await db.SaveChangesAsync();
        return envio;
    }

    private void Saltar() =>
        Skip.If(string.IsNullOrWhiteSpace(CadenaBase), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");

    private SistemaEnviosDbContext Contexto() =>
        new(new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(_cadena).Options);

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(CadenaBase)) return;

        var maestra = new SqlConnectionStringBuilder(CadenaBase) { InitialCatalog = "master" };
        await using (var conexion = new SqlConnection(maestra.ConnectionString))
        {
            await conexion.OpenAsync();
            await using var crear = conexion.CreateCommand();
            crear.CommandText = $"CREATE DATABASE [{_baseDesechable}]";
            await crear.ExecuteNonQueryAsync();
        }

        _cadena = new SqlConnectionStringBuilder(CadenaBase) { InitialCatalog = _baseDesechable }.ConnectionString;

        await using var db = Contexto();
        await db.Database.EnsureCreatedAsync();

        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(filial, tecnologia, estado);
        await db.SaveChangesAsync();

        _filialId = filial.UbicacionId;
        _tecnologiaId = tecnologia.UbicacionId;
        _estadoId = estado.EstadoEnvioId;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(CadenaBase)) return;

        var maestra = new SqlConnectionStringBuilder(CadenaBase) { InitialCatalog = "master" };
        await using var conexion = new SqlConnection(maestra.ConnectionString);
        await conexion.OpenAsync();
        await using var borrar = conexion.CreateCommand();
        borrar.CommandText =
            $"ALTER DATABASE [{_baseDesechable}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_baseDesechable}]";
        await borrar.ExecuteNonQueryAsync();
    }
}
