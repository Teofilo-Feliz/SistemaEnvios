using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;

using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El alcance no es una sola dimensión. Una filial ve lo suyo; Transportación ve las etapas
/// que custodia en todas las filiales, pero solo del transporte institucional (el privado va
/// directo a Tecnología); y un usuario de Tecnología ve todo.
/// </summary>
public sealed class AlcancePerfilesTests
{
    private static readonly Guid UsuarioId = Guid.Parse("d41f8b02-95c3-4a7e-8f61-27a0be5c9d33");
    private const string ClaimSantiago = "41,SANTIAGO";
    private const string PosicionTransportacion = "Encargado de Transportacion";
    private const string PosicionTecnologia = "Programador Senior";

    [Fact]
    public async Task Transportacion_VeEnvioEnSuEtapaDeOtraFilial()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnTransito, EstrategiaTransporteEnum.TransportacionInstitucional);

        var visibles = await ListarAsync(db, Transportacion());

        Assert.Single(visibles);
    }

    [Fact]
    public async Task Transportacion_NoVeEnvioQueTodaviaEstaEnLaFilial()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnFilial, EstrategiaTransporteEnum.TransportacionInstitucional);

        var visibles = await ListarAsync(db, Transportacion());

        Assert.Empty(visibles);
    }

    [Fact]
    public async Task Transportacion_NoVeEnvioDeTransportePrivado()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnTransito, EstrategiaTransporteEnum.EntregaDirectaTecnologia);

        var visibles = await ListarAsync(db, Transportacion());

        Assert.Empty(visibles);
    }

    [Fact]
    public async Task UsuarioDeTecnologia_ResuelvePerfilGlobal()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnFilial, EstrategiaTransporteEnum.EntregaDirectaTecnologia);

        var perfil = await AlcanceDePrueba.Crear(db, Tecnologia())
            .ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Global, perfil);
    }

    [Fact]
    public async Task UsuarioDeTecnologia_VeEnvioQueNoTocaSuUbicacion()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnFilial, EstrategiaTransporteEnum.EntregaDirectaTecnologia);
        await SembrarEnvioEntreFilialesAsync(db);

        var visibles = await ListarAsync(db, Tecnologia());

        Assert.Equal(2, visibles.Count);
    }

    [Fact]
    public async Task UsuarioDeFilial_NoVePerfilGlobal()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnFilial, EstrategiaTransporteEnum.EntregaDirectaTecnologia);

        var perfil = await AlcanceDePrueba.Crear(db, new FakeUserContext(UsuarioId, ClaimSantiago))
            .ResolverPerfilAsync();

        Assert.Equal(PerfilAlcance.Filial, perfil);
    }

    [Fact]
    public async Task UsuarioDeFilial_SigueViendoSoloLoSuyo()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db, EstadoEnvioCodigos.EnTransito, EstrategiaTransporteEnum.TransportacionInstitucional);

        var visibles = await ListarAsync(db, new FakeUserContext(UsuarioId, ClaimSantiago));

        Assert.Empty(visibles);
    }

    private static FakeUserContext Transportacion() =>
        new(UsuarioId, ClaimSantiago, roles: [PosicionTransportacion]);

    private static FakeUserContext Tecnologia() =>
        new(UsuarioId, ClaimSantiago, roles: [PosicionTecnologia]);

    private static async Task<List<Envio>> ListarAsync(SistemaEnviosDbContext db, IUserContext usuario)
    {
        var alcance = AlcanceDePrueba.Crear(db, usuario);
        var query = await alcance.FiltrarAsync(db.Envios.AsNoTracking());
        return await query.ToListAsync();
    }

    private static async Task SembrarAsync(
        SistemaEnviosDbContext db,
        string codigoEstado,
        EstrategiaTransporteEnum estrategia)
    {
        var santoDomingo = new Ubicacion { Nombre = "Santo Domingo", CodigoCentro = "FILIAL-SANTO-DOMINGO", FilialExternaId = 30, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "FILIAL-SANTIAGO", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TECNOLOGIA", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = codigoEstado, Nombre = codigoEstado, Activo = true };
        var tipo = new TipoTransporte { Codigo = "T", Nombre = "Tipo", Estrategia = estrategia, Activo = true };
        db.PerfilesPorPosicion.AddRange(
            new PerfilPosicion { Posicion = PosicionTransportacion, Perfil = (byte)PerfilAlcance.Transportacion },
            new PerfilPosicion { Posicion = PosicionTecnologia, Perfil = (byte)PerfilAlcance.Global });
        db.AddRange(santoDomingo, santiago, tecnologia, estado, tipo);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-PERFIL1",
            UbicacionOrigenId = santoDomingo.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        db.Transportes.Add(new Transporte { EnvioId = envio.EnvioId, TipoTransporteId = tipo.TipoTransporteId });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Envío insertado directamente entre dos filiales. El dominio no lo permite crear por
    /// servicio, pero sirve para distinguir "ve todo" de "ve lo que toca su ubicación".
    /// </summary>
    private static async Task SembrarEnvioEntreFilialesAsync(SistemaEnviosDbContext db)
    {
        var origen = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 30);
        var destino = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 41);
        var estado = await db.EstadosEnvio.FirstAsync();
        db.Envios.Add(new Envio
        {
            NumeroEnvio = "ENV-2026-PERFIL2",
            UbicacionOrigenId = origen.UbicacionId,
            UbicacionDestinoId = destino.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        });
        await db.SaveChangesAsync();
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}