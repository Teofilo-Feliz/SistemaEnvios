using SistemaEnvios.Application.DTOs.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.DTOs.Incidencias;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Application.Validators.Incidencias;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Repositories.Envios;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Infrastructure.Services.Dashboard;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Infrastructure.Services.Incidencias;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Infrastructure.Services.Transportes;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Un permiso dice QUE puede hacer el usuario, nunca SOBRE CUAL envío. Estas pruebas fijan que
/// ningún servicio opere sobre un envío de otra filial, aunque el permiso esté concedido.
/// </summary>
public sealed class AlcanceServiciosTests
{
    private static readonly Guid UsuarioId = Guid.Parse("6c1e2f3a-77bd-4f52-9a80-3c5b8d21ef44");

    // El usuario pertenece a Santiago (41); el envío es de Santo Domingo (30).
    private const string ClaimSantiago = "41,SANTIAGO";

    [Fact]
    public async Task EstadoEnvio_EntregarATransportacion_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new EstadoEnvioService(db, new UnitOfWork(db), usuario, alcance)
            .EntregarATransportacionAsync(envio.EnvioId);

        AssertFueraDeAlcance(resultado);
    }

    [Fact]
    public async Task EstadoEnvio_Cambiar_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new EstadoEnvioService(db, new UnitOfWork(db), usuario, alcance)
            .CambiarAsync(new CambiarEstadoEnvioRequest { EnvioId = envio.EnvioId, EstadoDestinoId = envio.EstadoEnvioId + 1 });

        AssertFueraDeAlcance(resultado);
    }

    [Fact]
    public async Task Recepcion_Crear_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new RecepcionService(
                db, new UnitOfWork(db),
                new CrearRecepcionRequestValidator(),
                new VerificarEquipoRequestValidator(),
                new AsignarTecnicoRequestValidator(),
                usuario, alcance)
            .CrearAsync(new CrearRecepcionRequest { EnvioId = envio.EnvioId });

        AssertFueraDeAlcance(resultado);
    }

    [Fact]
    public async Task EnvioEquipo_ListarPorEnvio_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new EnvioEquipoService(
                new EnvioEquipoRepository(db), new UnitOfWork(db),
                new AgregarEquipoEnvioRequestValidator(),
                new ActualizarEnvioEquipoRequestValidator(),
                db, usuario, alcance,
                new CasoEquipoService(db, new UnitOfWork(db), usuario, alcance))
            .ListarPorEnvioAsync(envio.EnvioId, new ParametrosPaginaSimple());

        AssertFueraDeAlcance(resultado);
    }

    [Fact]
    public async Task Transporte_ObtenerPorEnvio_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new TransporteService(
                db, new UnitOfWork(db),
                new CrearTransporteRequestValidator(),
                new ActualizarTransporteRequestValidator(),
                usuario, alcance)
            .ObtenerPorEnvioAsync(envio.EnvioId);

        AssertFueraDeAlcance(resultado);
    }

    [Fact]
    public async Task Incidencia_ListarPorEnvio_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new IncidenciaService(
                db, new UnitOfWork(db), new CrearIncidenciaRequestValidator(), usuario, alcance)
            .ListarPorEnvioAsync(envio.EnvioId, new ParametrosPaginaSimple());

        AssertFueraDeAlcance(resultado);
    }

    [Fact]
    public async Task Dashboard_NoCuentaEnviosDeOtraFilial()
    {
        await using var db = CrearContexto();
        await SembrarAsync(db);
        var (usuario, alcance) = Contexto(db);

        var resultado = await new DashboardService(db, alcance, usuario).ObtenerAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, resultado.Value!.Summary.TotalEnvios);
    }

    [Fact]
    public async Task Historial_ListarPorEnvio_RechazaEnvioDeOtraFilial()
    {
        await using var db = CrearContexto();
        var envio = await SembrarAsync(db);
        var (_, alcance) = Contexto(db);

        var resultado = await new HistorialEstadoEnvioService(db, alcance)
            .ListarPorEnvioAsync(envio.EnvioId, new ParametrosPaginaSimple());

        AssertFueraDeAlcance(resultado);
    }

    private static void AssertFueraDeAlcance(Result resultado)
    {
        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    private static (IUserContext Usuario, IAlcanceEnvios Alcance) Contexto(SistemaEnviosDbContext db)
    {
        var usuario = new FakeUserContext(UsuarioId, ClaimSantiago);
        return (usuario, new AlcanceEnvios(db, usuario));
    }

    private static async Task<Envio> SembrarAsync(SistemaEnviosDbContext db)
    {
        var santoDomingo = new Ubicacion { Nombre = "Santo Domingo", CodigoCentro = "FILIAL-SANTO-DOMINGO", FilialExternaId = 30, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "FILIAL-SANTIAGO", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TECNOLOGIA", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(santoDomingo, santiago, tecnologia, estado);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-AJENO01",
            UbicacionOrigenId = santoDomingo.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();
        return envio;
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
