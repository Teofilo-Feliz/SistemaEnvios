using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Varias personas operando a la vez sobre lo mismo. No sirve el proveedor InMemory: no aplica
/// índices únicos, así que dos inserciones en conflicto pasarían las dos.
///
/// A diferencia del resto de las pruebas de integración, estas escriben, y por eso montan su
/// propia base desechable en vez de tocar aquella a la que apunte SISTEMAENVIOS_TEST_SQL.
/// </summary>
public sealed class ConcurrenciaSqlServerTests : IAsyncLifetime
{
    private const int Usuarios = 8;
    private static string? CadenaBase => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    private readonly string _baseDesechable = $"ADRTrack_Concurrencia_{Guid.NewGuid():N}";
    private string _cadena = string.Empty;
    private int _filialId;
    private int _tecnologiaId;
    private int _tipoEquipoId;
    private int _estadoDespachadoId;
    private int _estadoEnFilialId;

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
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(filial, tecnologia, tipo);

        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var despachado = new EstadoEnvio
        {
            Codigo = EstadoEnvioCodigos.DespachadoTransportePrivado,
            Nombre = "Entregado a transporte privado",
            Activo = true,
        };
        db.EstadosEnvio.AddRange(enFilial, despachado);
        await db.SaveChangesAsync();

        db.TransicionesEstadoEnvio.Add(new TransicionEstadoEnvio
        {
            EstadoOrigenId = enFilial.EstadoEnvioId,
            EstadoDestinoId = despachado.EstadoEnvioId,
            Activo = true,
        });
        await db.SaveChangesAsync();

        _filialId = filial.UbicacionId;
        _tecnologiaId = tecnologia.UbicacionId;
        _tipoEquipoId = tipo.TipoEquipoId;
        _estadoDespachadoId = despachado.EstadoEnvioId;
        _estadoEnFilialId = enFilial.EstadoEnvioId;

        AlcanceDePrueba.Sembrar(db);

        for (var i = 0; i < Usuarios; i++)
        {
            db.Equipos.Add(new Equipo
            {
                TipoEquipoId = _tipoEquipoId,
                UbicacionActualId = _filialId,
                Marca = "Dell",
                Modelo = "Latitude",
                NumeroSerie = $"SN-CONC-{i}",
            });
        }
        await db.SaveChangesAsync();
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

    /// <summary>Ocho personas con el mismo ticket y un equipo distinto cada una.</summary>
    [SkippableFact]
    public async Task ElMismoTicketALaVez_SoloAbreUnCaso()
    {
        Saltar();
        var equipos = await EquiposAsync();

        var resultados = await EnParaleloAsync(i => new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            Observaciones = "Carrera de tickets",
            Equipos = [new EquipoEnvioInicialRequest { EquipoId = equipos[i], NumeroTicket = "770001" }],
        });

        Assert.Equal(1, resultados.Count(x => x.IsSuccess));

        await using var db = Contexto();
        Assert.Equal(1, await db.EnvioEquipos.CountAsync(x => x.NumeroTicket == "770001"));
    }

    /// <summary>Ocho personas mandando el mismo equipo en envíos distintos.</summary>
    [SkippableFact]
    public async Task ElMismoEquipoALaVez_SoloViajaEnUnEnvio()
    {
        Saltar();
        var equipos = await EquiposAsync();
        var disputado = equipos[0];

        var resultados = await EnParaleloAsync(i => new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            Observaciones = "Carrera de equipos",
            Equipos = [new EquipoEnvioInicialRequest { EquipoId = disputado, NumeroTicket = $"78000{i}" }],
        });

        Assert.Equal(1, resultados.Count(x => x.IsSuccess));

        await using var db = Contexto();
        Assert.Equal(1, await db.ReservasEquipoEnvio.CountAsync(x => x.EquipoId == disputado));
    }

    /// <summary>El envío y sus equipos entran juntos o no entra ninguno.</summary>
    [SkippableFact]
    public async Task ElQuePierdeNoDejaUnEnvioVacio()
    {
        Saltar();
        var equipos = await EquiposAsync();

        await EnParaleloAsync(i => new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            Observaciones = "Carrera de tickets",
            Equipos = [new EquipoEnvioInicialRequest { EquipoId = equipos[i], NumeroTicket = "790001" }],
        });

        await using var db = Contexto();
        Assert.Equal(0, await db.Envios.CountAsync(x => !x.Equipos.Any()));
    }

    /// <summary>
    /// Quién frena la carrera y qué lee quien la pierde. Medido: de siete perdedores, a los siete
    /// los para el índice de la base y la comprobación previa del servicio no atrapa a ninguno.
    /// Por eso el mensaje de ese camino tiene que nombrar el ticket: es el que ve la gente.
    /// </summary>
    [SkippableFact]
    public async Task ElQuePierdeRecibeUnConflicto()
    {
        Saltar();
        var equipos = await EquiposAsync();

        var resultados = await EnParaleloAsync(i => new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            Observaciones = "Carrera de tickets",
            Equipos = [new EquipoEnvioInicialRequest { EquipoId = equipos[i], NumeroTicket = "800001" }],
        });

        var perdedores = resultados.Where(x => x.IsFailure).ToArray();
        Assert.NotEmpty(perdedores);
        Assert.All(perdedores, x => Assert.Equal(ErrorType.Conflict, x.ErrorType));
        Assert.All(perdedores, x => Assert.Contains("800001", x.Error!));
        Assert.All(perdedores, x => Assert.Contains("ya fue utilizado", x.Error!));
    }

    /// <summary>
    /// Ocho personas moviendo el mismo envío, o una con el botón pulsado ocho veces. Aquí no hay
    /// índice único: lo que arbitra es el RowVersion del envío.
    /// </summary>
    [SkippableFact]
    public async Task ElMismoEnvioMovidoALaVez_SoloAvanzaUnaVez()
    {
        Saltar();
        var envioId = await UnEnvioAsync();

        var tareas = Enumerable.Range(0, Usuarios).Select(async _ =>
        {
            await using var db = Contexto();
            var usuario = FakeUserContext.Global(Guid.NewGuid());
            var servicio = new EstadoEnvioService(
                db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario));
            return await servicio.CambiarAsync(new CambiarEstadoEnvioRequest
            {
                EnvioId = envioId,
                EstadoDestinoId = _estadoDespachadoId,
                Observaciones = "Carrera de estados",
            });
        }).ToArray();

        var resultados = await Task.WhenAll(tareas);

        Assert.Equal(1, resultados.Count(x => x.IsSuccess));

        await using var db2 = Contexto();
        Assert.Equal(2, await db2.HistorialEstadosEnvio.CountAsync(x => x.EnvioId == envioId));
    }

    /// <summary>
    /// Ocho descartes del mismo equipo a la vez. El cierre se declara idempotente, pero la
    /// comprobación solo cubre las llamadas en orden: simultáneas, la segunda choca con el
    /// RowVersion y antes esa excepción se escapaba del servicio.
    /// </summary>
    [SkippableFact]
    public async Task ElMismoEquipoDescartadoALaVez_SeCierraUnaSolaVez()
    {
        Saltar();
        var equipoId = await UnEquipoEnTecnologiaConCasoAsync();

        var tareas = Enumerable.Range(0, Usuarios).Select(async i =>
        {
            await using var db = Contexto();
            var usuario = FakeUserContext.Global(Guid.NewGuid());
            var servicio = new CasoEquipoService(
                db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario));
            return await servicio.DescartarAsync(equipoId, $"Motivo del usuario {i}");
        }).ToArray();

        var resultados = await Task.WhenAll(tareas);

        Assert.All(resultados, x => Assert.True(
            x.IsSuccess || x.ErrorType == ErrorType.Conflict, x.Error));

        await using var db2 = Contexto();
        var apertura = await db2.EnvioEquipos.AsNoTracking()
            .SingleAsync(x => x.EquipoId == equipoId && x.EnvioEquipoOrigenId == null);

        Assert.NotNull(apertura.FechaCierreCaso);
        Assert.StartsWith("Descartado de la filial: Motivo del usuario", apertura.MotivoCierreCaso);
    }

    private async Task<int> UnEquipoEnTecnologiaConCasoAsync()
    {
        await using var db = Contexto();
        var equipo = await db.Equipos.OrderBy(x => x.EquipoId).FirstAsync();
        equipo.UbicacionActualId = _tecnologiaId;

        var envio = new Envio
        {
            NumeroEnvio = $"ENV-CASO-{Guid.NewGuid():N}"[..18],
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            EstadoEnvioId = _estadoEnFilialId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = Guid.NewGuid(),
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "820001",
            UsuarioSolicitanteId = envio.UsuarioSolicitanteId,
            Observaciones = "Apertura del caso",
        });
        await db.SaveChangesAsync();
        return equipo.EquipoId;
    }

    private async Task<int> UnEnvioAsync()
    {
        var equipos = await EquiposAsync();
        await using var db = Contexto();
        var resultado = await Servicio(db).CrearConEquiposAsync(new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = _filialId,
            UbicacionDestinoId = _tecnologiaId,
            Observaciones = "Envío para mover",
            Equipos = [new EquipoEnvioInicialRequest { EquipoId = equipos[0], NumeroTicket = "810001" }],
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        return resultado.Value!.EnvioId;
    }

    private void Saltar() =>
        Skip.If(string.IsNullOrWhiteSpace(CadenaBase), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");

    private async Task<int[]> EquiposAsync()
    {
        await using var db = Contexto();
        return await db.Equipos.OrderBy(x => x.EquipoId).Select(x => x.EquipoId).ToArrayAsync();
    }

    /// <summary>
    /// Las N peticiones a la vez, cada una con su propio contexto: uno compartido serializaría
    /// los accesos y la carrera no llegaría a la base, que es donde se decide.
    /// </summary>
    private async Task<Result<EnvioResponse>[]> EnParaleloAsync(Func<int, CrearEnvioConEquiposRequest> peticion)
    {
        var salida = new SemaphoreSlim(0, Usuarios);
        var tareas = Enumerable.Range(0, Usuarios).Select(async i =>
        {
            await using var db = Contexto();
            var servicio = Servicio(db);
            var solicitud = peticion(i);

            salida.Release();
            await salida.WaitAsync();
            salida.Release();

            return await servicio.CrearConEquiposAsync(solicitud);
        }).ToArray();

        return await Task.WhenAll(tareas);
    }

    private SistemaEnviosDbContext Contexto() =>
        new(new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(_cadena).Options);

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var usuario = FakeUserContext.Global(Guid.NewGuid());
        var alcance = AlcanceDePrueba.Crear(db, usuario);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, alcance,
            new CasoEquipoService(db, new UnitOfWork(db), usuario, alcance),
            new ValidadorTicketDePrueba());
    }
}
