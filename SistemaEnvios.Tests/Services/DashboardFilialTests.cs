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
/// El tablero de una filial cuenta en la base y solo sobre lo suyo. La regla de alcance no es
/// cosmética aquí: si el conteo se hiciera sobre todos los envíos, una filial vería el volumen
/// de las otras 33 en sus propias tarjetas.
/// </summary>
public sealed class DashboardFilialTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task CuentaSoloLosEnviosDeSuFilial()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ObtenerFilialAsync();

        Assert.True(resultado.IsSuccess, resultado.Error);
        // Azua tiene 3 envíos; Santiago tiene 2 que no deben contarse.
        Assert.Equal(3, resultado.Value!.PorEtapa.Sum(x => x.Total));
    }

    [Fact]
    public async Task SeparaLoQueEstaPorSalirDeLoQueVieneDeVuelta()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ObtenerFilialAsync();

        var enFilial = resultado.Value!.PorEtapa.Single(x => x.Codigo == EstadoEnvioCodigos.EnFilial);
        Assert.Equal(1, enFilial.Total);
        // El que viene de vuelta va en la misma etapa pero en el otro sentido.
        var deVuelta = resultado.Value.PorEtapa.Single(
            x => x.Codigo == EstadoEnvioCodigos.EnTransito && x.Direccion == (int)DireccionEnvioEnum.HaciaFilial);
        Assert.Equal(1, deVuelta.Total);
    }

    [Fact]
    public async Task InformaCuantosEquiposTieneYCuantosEstanFuera()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ObtenerFilialAsync();

        // Dos equipos en Azua y uno en Tecnología con su caso todavía abierto.
        Assert.Equal(2, resultado.Value!.EquiposEnFilial);
        Assert.Equal(1, resultado.Value.EquiposFuera);
    }

    [Fact]
    public async Task UnPerfilSinFilialNoObtieneTablero()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db, "Programador Senior").ObtenerFilialAsync();

        Assert.True(resultado.IsFailure);
    }

    private static DashboardService Servicio(SistemaEnviosDbContext db, string posicion = "Administrador de Filial")
    {
        IUserContext u = new FakeUserContext(UsuarioId, "1,AZUA", position: posicion);
        return new DashboardService(db, AlcanceDePrueba.Crear(db, u), u);
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(azua, santiago, tecnologia, enFilial, transito, tipo);
        db.AddRange(
            new PerfilPosicion { Posicion = "Administrador de Filial", Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = (byte)PerfilAlcance.Global });
        await db.SaveChangesAsync();

        Envio Nuevo(string numero, int origen, int destino, EstadoEnvio estado, DireccionEnvioEnum direccion) => new()
        {
            NumeroEnvio = numero,
            UbicacionOrigenId = origen,
            UbicacionDestinoId = destino,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = UsuarioId,
            FechaCreacion = DateTime.UtcNow
        };
        var deAzua = Nuevo("ENV-AZUA-1", azua.UbicacionId, tecnologia.UbicacionId, enFilial, DireccionEnvioEnum.HaciaTecnologia);
        db.Envios.AddRange(
            deAzua,
            Nuevo("ENV-AZUA-2", azua.UbicacionId, tecnologia.UbicacionId, transito, DireccionEnvioEnum.HaciaTecnologia),
            Nuevo("ENV-AZUA-3", tecnologia.UbicacionId, azua.UbicacionId, transito, DireccionEnvioEnum.HaciaFilial),
            Nuevo("ENV-STGO-1", santiago.UbicacionId, tecnologia.UbicacionId, enFilial, DireccionEnvioEnum.HaciaTecnologia),
            Nuevo("ENV-STGO-2", santiago.UbicacionId, tecnologia.UbicacionId, transito, DireccionEnvioEnum.HaciaTecnologia));
        await db.SaveChangesAsync();

        var fuera = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = tecnologia.UbicacionId, Marca = "Dell", Modelo = "L3", NumeroSerie = "SN-FUERA" };
        db.Equipos.AddRange(
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-A1" },
            new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Dell", Modelo = "L2", NumeroSerie = "SN-A2" },
            fuera);
        await db.SaveChangesAsync();

        // El equipo que está fuera tiene su caso abierto, colgado del envío de Azua.
        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = deAzua.EnvioId,
            EquipoId = fuera.EquipoId,
            NumeroTicket = "900",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Apertura del caso"
        });
        await db.SaveChangesAsync();
        return db;
    }
}
