using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Casos;
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
/// El desplegable de equipos cuando Tecnología arma un envío hacia una filial.
///
/// La regla: solo aparece lo que una filial está esperando, es decir, equipos con caso abierto.
/// Lo que está en Tecnología sin caso —un descarte, un equipo todavía sin asignar— no es de
/// nadie y se asigna por otra vía, así que no se mezcla aquí.
/// </summary>
public sealed class EquiposEnTecnologiaTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    /// <summary>Sin filtro, lo de todas las filiales: es lo que se ve antes de elegir destino.</summary>
    [Fact]
    public async Task SinFiltroTraeLoDeTodasLasFiliales()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(new ConsultarEquiposEnTecnologiaRequest());

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(["TEC-AZUA", "TEC-SANTIAGO"], Seriales(resultado.Value!.Items));
    }

    /// <summary>
    /// Un equipo en Tecnología sin caso abierto no es de ninguna filial. No sale ni con filtro
    /// ni sin él: se asigna por otra vía.
    /// </summary>
    [Fact]
    public async Task UnEquipoSinCasoNoAparece()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(new ConsultarEquiposEnTecnologiaRequest());

        Assert.DoesNotContain("TEC-LIBRE", Seriales(resultado.Value!.Items));
    }

    /// <summary>Un equipo que está en su filial no lo puede mandar Tecnología.</summary>
    [Fact]
    public async Task NoTraeEquiposQueNoEstanEnTecnologia()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(new ConsultarEquiposEnTecnologiaRequest());

        Assert.DoesNotContain("SN-EN-SU-FILIAL", Seriales(resultado.Value!.Items));
    }

    [Fact]
    public async Task FiltrarPorFilialTraeSoloLosDeEsaFilial()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(
            new ConsultarEquiposEnTecnologiaRequest { FilialId = Ubicacion(db, "Santiago") });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(["TEC-SANTIAGO"], Seriales(resultado.Value!.Items));
    }

    [Fact]
    public async Task FiltrarPorFilialDejaFueraLosDeOtra()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(
            new ConsultarEquiposEnTecnologiaRequest { FilialId = Ubicacion(db, "Santiago") });

        Assert.DoesNotContain("TEC-AZUA", Seriales(resultado.Value!.Items));
    }

    /// <summary>
    /// El ticket es lo que hace reconocible al equipo: dos máquinas del mismo modelo solo se
    /// distinguen por el serial, que nadie se sabe de memoria.
    /// </summary>
    [Fact]
    public async Task CadaEquipoConCasoTraeSuTicketYSuFilial()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(new ConsultarEquiposEnTecnologiaRequest());
        var equipo = resultado.Value!.Items.Single(x => x.NumeroSerie == "TEC-SANTIAGO");

        Assert.Equal("145", equipo.NumeroTicket);
        Assert.Equal(Ubicacion(db, "Santiago"), equipo.FilialId);
    }

    /// <summary>
    /// Descartar cierra el caso: la filial deja de ser dueña del equipo y el equipo deja de
    /// estar esperando volver a ninguna parte. Desaparece de este desplegable, que es lo que
    /// obliga a reasignarlo por la vía del descarte en vez de mandarlo de vuelta por descuido.
    /// </summary>
    [Fact]
    public async Task UnCasoCerradoSacaAlEquipoDeLaLista()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(
            new ConsultarEquiposEnTecnologiaRequest { FilialId = Ubicacion(db, "Santiago") });

        Assert.DoesNotContain("TEC-DESCARTADO", Seriales(resultado.Value!.Items));
    }

    /// <summary>
    /// La lista lleva los tickets de todas las filiales, así que no la ve una filial: sería el
    /// inventario completo de la sede en manos de quien solo debería ver lo suyo.
    /// </summary>
    [Fact]
    public async Task UnaFilialNoVeElInventarioDeLaSede()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db, "Asistente Administrativo")
            .ListarEquiposEnTecnologiaAsync(new ConsultarEquiposEnTecnologiaRequest());

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task BuscaPorNumeroDeTicket()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarEquiposEnTecnologiaAsync(
            new ConsultarEquiposEnTecnologiaRequest { Search = "145" });

        Assert.Equal(["TEC-SANTIAGO"], Seriales(resultado.Value!.Items));
    }

    private static string[] Seriales(IEnumerable<EquipoEnTecnologiaResponse> items) =>
        [.. items.Select(x => x.NumeroSerie ?? string.Empty).Order()];

    private static int Ubicacion(SistemaEnviosDbContext db, string nombre) =>
        db.Ubicaciones.First(x => x.Nombre == nombre).UbicacionId;

    private static CasoEquipoService Servicio(SistemaEnviosDbContext db, string rol = "Programador Senior")
    {
        IUserContext u = new FakeUserContext(UsuarioId, "1,AZUA", roles: [rol]);
        return new CasoEquipoService(db, new UnitOfWork(db), u, AlcanceDePrueba.Crear(db, u));
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

        var deSantiago = EnTecnologia("TEC-SANTIAGO", "L1");
        var deAzua = EnTecnologia("TEC-AZUA", "L2");
        var libre = EnTecnologia("TEC-LIBRE", "L3");
        var descartado = EnTecnologia("TEC-DESCARTADO", "L4");
        var enSuFilial = new Equipo
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = santiago.UbicacionId,
            Marca = "Dell",
            Modelo = "L5",
            NumeroSerie = "SN-EN-SU-FILIAL"
        };
        db.Equipos.AddRange(deSantiago, deAzua, libre, descartado, enSuFilial);
        await db.SaveChangesAsync();

        await AbrirCasoAsync(db, deSantiago, santiago, tecnologia, estado, "145", cerrado: false);
        await AbrirCasoAsync(db, deAzua, azua, tecnologia, estado, "900", cerrado: false);
        await AbrirCasoAsync(db, descartado, santiago, tecnologia, estado, "700", cerrado: true);
        return db;

        Equipo EnTecnologia(string serie, string modelo) => new()
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = tecnologia.UbicacionId,
            Marca = "Dell",
            Modelo = modelo,
            NumeroSerie = serie
        };
    }

    private static async Task AbrirCasoAsync(
        SistemaEnviosDbContext db, Equipo equipo, Ubicacion filial, Ubicacion tecnologia,
        EstadoEnvio estado, string ticket, bool cerrado)
    {
        var envio = new Envio
        {
            NumeroEnvio = $"ENV-{ticket}",
            UbicacionOrigenId = filial.UbicacionId,
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
            EquipoId = equipo.EquipoId,
            NumeroTicket = ticket,
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Apertura",
            FechaCierreCaso = cerrado ? DateTime.UtcNow : null,
            MotivoCierreCaso = cerrado ? "Descartado de la filial: prueba" : null
        });
        await db.SaveChangesAsync();
    }
}
