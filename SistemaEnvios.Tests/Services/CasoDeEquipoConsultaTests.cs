using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// La pantalla de envío pregunta por el caso del equipo para rellenar y bloquear el ticket. Esa
/// consulta expone el ticket de una filial, así que respeta el alcance: un usuario de Azua no
/// puede leer el caso de un equipo de Santiago.
/// </summary>
public sealed class CasoDeEquipoConsultaTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task DevuelveElCasoAbiertoParaQueLaPantallaHeredeElTicket()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db, "Programador Senior").ConsultarDeEquipoAsync(Equipo(db, "SN-SANTIAGO"));

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal("145", resultado.Value!.NumeroTicket);
    }

    [Fact]
    public async Task UnEquipoSinCasoDevuelveNadaYLaPantallaPideTicketNuevo()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db, "Programador Senior").ConsultarDeEquipoAsync(Equipo(db, "SN-LIBRE"));

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Null(resultado.Value);
    }

    [Fact]
    public async Task UnUsuarioDeOtraFilialNoPuedeLeerElCasoAjeno()
    {
        await using var db = await SembrarAsync();

        // El usuario es de Azua (1); el equipo y su caso son de Santiago (41).
        var resultado = await Servicio(db, "Asistente Administrativo").ConsultarDeEquipoAsync(Equipo(db, "SN-SANTIAGO"));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    private static int Equipo(SistemaEnviosDbContext db, string serie) =>
        db.Equipos.First(x => x.NumeroSerie == serie).EquipoId;

    private static CasoEquipoService Servicio(SistemaEnviosDbContext db, string posicion)
    {
        IUserContext u = new FakeUserContext(UsuarioId, "1,AZUA", position: posicion);
        return new CasoEquipoService(db, new UnitOfWork(db), u, new AlcanceEnvios(db, u));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(santiago, azua, tecnologia, tipo, estado);
        db.AddRange(
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = (byte)PerfilAlcance.Global },
            new PerfilPosicion { Posicion = "Asistente Administrativo", Perfil = (byte)PerfilAlcance.Filial });
        await db.SaveChangesAsync();

        var conCaso = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = santiago.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-SANTIAGO" };
        var libre = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Dell", Modelo = "L2", NumeroSerie = "SN-LIBRE" };
        db.Equipos.AddRange(conCaso, libre);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-CASO",
            UbicacionOrigenId = santiago.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = conCaso.EquipoId,
            NumeroTicket = "145",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Apertura"
        });
        await db.SaveChangesAsync();
        return db;
    }
}
