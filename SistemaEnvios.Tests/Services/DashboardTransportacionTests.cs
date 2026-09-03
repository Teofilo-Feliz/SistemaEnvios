using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Dashboard;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El dashboard contaba en el navegador sobre la lista completa de envíos, así que para dibujar
/// unos totales había que descargarlos todos. Los totales se cuentan en la base; lo que viaja
/// son los números.
/// </summary>
public sealed class DashboardTransportacionTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8e4d2c19-6a35-4f70-b1c8-3d9f0a7e5b26");

    [Fact]
    public async Task CuentaLosEnviosPorEtapaSinTraerlos()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ObtenerTransportacionAsync();

        Assert.True(resultado.IsSuccess, resultado.Error);
        var enTransito = resultado.Value!.PorEtapa.Single(x => x.Codigo == EstadoEnvioCodigos.EnTransito);
        Assert.Equal(3, enTransito.Total);
        var entregados = resultado.Value.PorEtapa.Single(x => x.Codigo == EstadoEnvioCodigos.EntregadoATransportacion);
        Assert.Equal(2, entregados.Total);
    }

    [Fact]
    public async Task AgrupaPorTipoDeTransporteIncluyendoLosQueNoTienen()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ObtenerTransportacionAsync();

        var porTipo = resultado.Value!.PorTipoTransporte.ToDictionary(x => x.Label, x => x.Total);
        Assert.Equal(3, porTipo["Camión institucional"]);
        Assert.Equal(2, porTipo["Sin transporte"]);
    }

    [Fact]
    public async Task ElTotalGeneralNoCuentaEtapasAjenasATransportacion()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ObtenerTransportacionAsync();

        // Los 2 envíos EN_FILIAL no son de Transportación: 3 en tránsito + 2 entregados.
        Assert.Equal(5, resultado.Value!.TotalEnEtapas);
    }

    private static DashboardService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext usuario = new FakeUserContext(UsuarioId, roles: ["AdministradorGlobal"]);
        return new DashboardService(db, new AlcanceEnvios(db, usuario), usuario);
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var entregado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EntregadoATransportacion, Nombre = "Entregado a Transportación", Activo = true };
        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var tipo = new TipoTransporte { Codigo = "CAM", Nombre = "Camión institucional", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        db.AddRange(filial, tecnologia, transito, entregado, enFilial, tipo);
        await db.SaveChangesAsync();

        Envio Nuevo(int i, EstadoEnvio estado) => new()
        {
            NumeroEnvio = $"ENV-{i:D4}",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
            FechaCreacion = DateTime.UtcNow.AddMinutes(-i)
        };

        var conTransporte = new[] { Nuevo(1, transito), Nuevo(2, transito), Nuevo(3, transito) };
        var sinTransporte = new[] { Nuevo(4, entregado), Nuevo(5, entregado) };
        var fuera = new[] { Nuevo(6, enFilial), Nuevo(7, enFilial) };
        db.Envios.AddRange([.. conTransporte, .. sinTransporte, .. fuera]);
        await db.SaveChangesAsync();

        db.Transportes.AddRange(conTransporte.Select(envio => new Transporte
        {
            EnvioId = envio.EnvioId,
            TipoTransporteId = tipo.TipoTransporteId,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacionId = UsuarioId
        }));
        await db.SaveChangesAsync();
        return db;
    }
}
