using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Tecnología ve todo, pero necesita acotar por filial para tener control. El filtro toma la
/// ubicación en cualquiera de los dos extremos: un envío "pertenece" a la filial lo mande o lo reciba.
/// </summary>
public sealed class FiltroPorFilialTests
{
    private static readonly Guid UsuarioId = Guid.Parse("5e7d9c31-8b2a-4f60-a1c3-6d4e8b0f2a95");

    [Fact]
    public async Task FiltrarPorUbicacion_DevuelveSoloLosEnviosDeEsaFilial()
    {
        await using var db = CrearContexto();
        var (santiago, _) = await SembrarAsync(db);

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            UbicacionId = santiago.UbicacionId
        });

        Assert.True(resultado.IsSuccess);
        var envio = Assert.Single(resultado.Value!.Items);
        Assert.Equal(santiago.UbicacionId, envio.UbicacionOrigenId);
    }

    [Fact]
    public async Task SinFiltro_DevuelveTodosLosEnvios()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db);

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest());

        Assert.Equal(2, resultado.Value!.Items.Count);
    }

    [Fact]
    public async Task FiltrarPorUbicacionDeDestino_TambienCuenta()
    {
        await using var db = CrearContexto();
        var (_, tecnologia) = await SembrarAsync(db);

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            UbicacionId = tecnologia.UbicacionId
        });

        Assert.Equal(2, resultado.Value!.Items.Count);
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var usuario = new FakeUserContext(UsuarioId, "30,SEDE", position: "Programador Senior");
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario),
            new CasoEquipoService(db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario)), ValidadorTicketDePrueba.QueAcepta());
    }

    private static async Task<(Ubicacion Santiago, Ubicacion Tecnologia)> SembrarAsync(SistemaEnviosDbContext db)
    {
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "FILIAL-SANTIAGO", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var santoDomingo = new Ubicacion { Nombre = "Santo Domingo", CodigoCentro = "FILIAL-SD", FilialExternaId = 30, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TECNOLOGIA", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(santiago, santoDomingo, tecnologia, estado);
        db.PerfilesPorPosicion.Add(new PerfilPosicion { Posicion = "Programador Senior", Perfil = 1 });
        await db.SaveChangesAsync();

        db.Envios.AddRange(
            Envio(santiago.UbicacionId, tecnologia.UbicacionId, estado.EstadoEnvioId, "ENV-2026-SANTIAGO"),
            Envio(santoDomingo.UbicacionId, tecnologia.UbicacionId, estado.EstadoEnvioId, "ENV-2026-SANTODOM"));
        await db.SaveChangesAsync();
        return (santiago, tecnologia);
    }

    private static Envio Envio(int origen, int destino, int estado, string numero) => new()
    {
        NumeroEnvio = numero,
        UbicacionOrigenId = origen,
        UbicacionDestinoId = destino,
        EstadoEnvioId = estado,
        Direccion = DireccionEnvioEnum.HaciaTecnologia,
        UsuarioSolicitanteId = UsuarioId
    };

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
