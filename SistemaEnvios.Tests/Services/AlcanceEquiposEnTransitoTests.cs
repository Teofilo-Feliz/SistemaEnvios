using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Equipos;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Una filial tiene que poder ver el equipo que le viene en camino: si no, no puede recibirlo,
/// que es justo el momento en que necesita mirarlo. Acotar los equipos solo a los que están
/// físicamente en su filial dejaba la pantalla de recepción en "no tienes permisos".
///
/// Ver no es tocar: mover un equipo sigue exigiendo que esté en su filial.
/// </summary>
public sealed class AlcanceEquiposEnTransitoTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task LaFilialVeElEquipoQueLeVieneEnCaminoAunqueSigaEnTecnologia()
    {
        await using var db = await SembrarAsync();
        var equipo = await Equipo(db, "SN-EN-CAMINO");

        var resultado = await Servicio(db).ObtenerAsync(equipo);

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task TambienLoEncuentraEnElListado()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarAsync(new ConsultarEquiposRequest());

        Assert.Contains(resultado.Value!.Items, x => x.NumeroSerie == "SN-EN-CAMINO");
    }

    [Fact]
    public async Task ElEquipoDeOtraFilialSigueFueraDeSuAlcance()
    {
        await using var db = await SembrarAsync();
        var ajeno = await Equipo(db, "SN-AJENO");

        var resultado = await Servicio(db).ObtenerAsync(ajeno);

        Assert.True(resultado.IsFailure);
    }

    [Fact]
    public async Task VerNoEsTocarElEquipoEnCaminoNoSePuedeMoverTodavia()
    {
        await using var db = await SembrarAsync();
        var enCamino = await db.Equipos.FirstAsync(x => x.NumeroSerie == "SN-EN-CAMINO");
        var suFilial = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 31);

        var resultado = await Servicio(db).ActualizarAsync(new ActualizarEquipoRequest
        {
            EquipoId = enCamino.EquipoId,
            TipoEquipoId = enCamino.TipoEquipoId,
            UbicacionActualId = suFilial.UbicacionId,
            Marca = enCamino.Marca,
            Modelo = enCamino.Modelo,
            NumeroSerie = enCamino.NumeroSerie,
        });

        // Está en Tecnología: quien lo mueve es la recepción cuando llegue, no una edición.
        Assert.True(resultado.IsFailure);
    }

    private static async Task<int> Equipo(SistemaEnviosDbContext db, string serie) =>
        (await db.Equipos.FirstAsync(x => x.NumeroSerie == serie)).EquipoId;

    private static EquipoService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext u = new FakeUserContext(UsuarioId, "31,SANTO DOMINGO ESTE", position: "Administrador de Filial");
        return new EquipoService(
            db, new UnitOfWork(db),
            new CrearEquipoRequestValidator(), new ActualizarEquipoRequestValidator(),
            u, AlcanceDePrueba.Crear(db, u));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var sde = new Ubicacion { Nombre = "Santo Domingo Este", CodigoCentro = "SDE", FilialExternaId = 31, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        db.AddRange(sde, azua, tecnologia, tipo, transito);
        await db.SaveChangesAsync();

        // El que viene en camino sigue en Tecnología hasta que la filial lo reciba.
        var enCamino = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = tecnologia.UbicacionId, Marca = "Lenovo", Modelo = "M70", NumeroSerie = "SN-EN-CAMINO" };
        var propio = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = sde.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-PROPIO" };
        var ajeno = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Dell", Modelo = "L2", NumeroSerie = "SN-AJENO" };
        db.Equipos.AddRange(enCamino, propio, ajeno);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-DEVUELTO",
            UbicacionOrigenId = tecnologia.UbicacionId,
            UbicacionDestinoId = sde.UbicacionId,
            EstadoEnvioId = transito.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaFilial,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = enCamino.EquipoId,
            NumeroTicket = "700",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Devolución"
        });
        await db.SaveChangesAsync();
        return db;
    }
}
