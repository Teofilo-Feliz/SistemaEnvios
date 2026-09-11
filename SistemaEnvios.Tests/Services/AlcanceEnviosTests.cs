using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
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
/// El claim "affiliate" de AuthManager llega como "30,SANTO DOMINGO (SEDE)": un id numérico
/// de filial más su nombre. CodigoCentro es un código de negocio local ("FILIAL-SANTO-DOMINGO"),
/// así que el cruce entre ambos mundos se hace por Ubicacion.FilialExternaId.
/// </summary>
public sealed class AlcanceEnviosTests
{
    private const string ClaimSantoDomingo = "30,SANTO DOMINGO (SEDE)";
    private const string ClaimSantiago = "41,SANTIAGO";
    private static readonly Guid UsuarioId = Guid.Parse("2b0f0f4c-0f3a-4a1b-9f2e-1d9a5c7b4e10");

    [Fact]
    public async Task Listar_UsuarioDeFilial_VeElEnvioDeSuFilial()
    {
        await using var db = CrearContexto();
        var (filial, _) = await SembrarEnvioDesdeFilialAsync(db, filialExternaId: 30);

        var resultado = await CrearServicio(db, ClaimSantoDomingo).ConsultarAsync(new ConsultarEnviosRequest());

        Assert.True(resultado.IsSuccess);
        var envio = Assert.Single(resultado.Value!.Items);
        Assert.Equal(filial.UbicacionId, envio.UbicacionOrigenId);
    }

    [Fact]
    public async Task Listar_UsuarioDeOtraFilial_NoVeElEnvioAjeno()
    {
        await using var db = CrearContexto();
        await SembrarEnvioDesdeFilialAsync(db, filialExternaId: 30);
        db.Ubicaciones.Add(new Ubicacion
        {
            Nombre = "Filial Santiago",
            CodigoCentro = "FILIAL-SANTIAGO",
            FilialExternaId = 41,
            Tipo = TipoUbicacionEnum.Filial,
            Activo = true
        });
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db, ClaimSantiago).ConsultarAsync(new ConsultarEnviosRequest());

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!.Items);
    }

    [Fact]
    public async Task Obtener_UsuarioDeFilial_AccedeASuEnvio()
    {
        await using var db = CrearContexto();
        var (_, envio) = await SembrarEnvioDesdeFilialAsync(db, filialExternaId: 30);

        var resultado = await CrearServicio(db, ClaimSantoDomingo).ObtenerAsync(envio.EnvioId);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(envio.EnvioId, resultado.Value!.EnvioId);
    }

    [Fact]
    public async Task Obtener_FilialSinMapear_FallaConMensajeExplicito()
    {
        await using var db = CrearContexto();
        var (_, envio) = await SembrarEnvioDesdeFilialAsync(db, filialExternaId: null);

        var resultado = await CrearServicio(db, ClaimSantoDomingo).ObtenerAsync(envio.EnvioId);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        Assert.Contains("30", resultado.Error);
    }

    [Fact]
    public async Task Listar_AdministradorGlobal_VeTodosLosEnvios()
    {
        await using var db = CrearContexto();
        await SembrarEnvioDesdeFilialAsync(db, filialExternaId: 30);

        var resultado = await
        CrearServicio(db, ClaimSantiago, "Programador Senior").ConsultarAsync(new ConsultarEnviosRequest());

        Assert.True(resultado.IsSuccess);
        Assert.Single(resultado.Value!.Items);
    }

    private static async Task<(Ubicacion Filial, Envio Envio)> SembrarEnvioDesdeFilialAsync(
        SistemaEnviosDbContext db,
        int? filialExternaId)
    {
        var filial = new Ubicacion
        {
            Nombre = "Filial Santo Domingo",
            CodigoCentro = "FILIAL-SANTO-DOMINGO",
            FilialExternaId = filialExternaId,
            Tipo = TipoUbicacionEnum.Filial,
            Activo = true
        };
        var tecnologia = new Ubicacion
        {
            Nombre = "Centro de Tecnología",
            CodigoCentro = "TECNOLOGIA",
            FilialExternaId = null,
            Tipo = TipoUbicacionEnum.Tecnologia,
            Activo = true
        };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(filial, tecnologia, estado);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-000001",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();
        return (filial, envio);
    }

    private static EnvioService CrearServicio(
        SistemaEnviosDbContext db,
        string affiliate,
        string? rol = null)
    {
        var userContext = new FakeUserContext(UsuarioId, affiliate, roles: rol is null ? [] : [rol]);
        return new EnvioService(
            new GenericRepository<Envio>(db),
            db,
            new UnitOfWork(db),
            new CrearEnvioRequestValidator(),
            new ActualizarEnvioRequestValidator(),
            userContext,
            AlcanceDePrueba.Crear(db, userContext), new CasoEquipoService(db, new UnitOfWork(db), userContext, AlcanceDePrueba.Crear(db, userContext)), ValidadorTicketDePrueba.QueAcepta());
    }

    private static SistemaEnviosDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SistemaEnviosDbContext(options);
    }
}