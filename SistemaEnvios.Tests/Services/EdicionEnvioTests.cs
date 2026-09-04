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
/// Un envío se puede editar mientras no haya empezado a moverse, en cualquiera de las dos
/// direcciones: EN_FILIAL (filial -> Tecnología) y PREPARACION_TECNOLOGIA (Tecnología ->
/// filial). Toda edición queda registrada en el historial.
/// </summary>
public sealed class EdicionEnvioTests
{
    private static readonly Guid UsuarioId = Guid.Parse("7f3a0c58-91d2-4e63-b8a4-05c7e1d9f206");

    [Fact]
    public async Task EnvioDesdeTecnologia_SePuedeEditarAntesDeMoverse()
    {
        await using var db = CrearContexto();
        var (envio, ubicaciones) = await SembrarAsync(db, EstadoEnvioCodigos.EnPreparacionTecnologia, DireccionEnvioEnum.HaciaFilial);

        var resultado = await Servicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = ubicaciones["TEC"].UbicacionId,
            UbicacionDestinoId = ubicaciones["FIL-B"].UbicacionId,
            Observaciones = "Destino corregido",
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(ubicaciones["FIL-B"].UbicacionId, envio.UbicacionDestinoId);
    }

    [Fact]
    public async Task EnvioDesdeFilial_SigueSiendoEditable()
    {
        await using var db = CrearContexto();
        var (envio, ubicaciones) = await SembrarAsync(db, EstadoEnvioCodigos.EnFilial, DireccionEnvioEnum.HaciaTecnologia);

        var resultado = await Servicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = ubicaciones["FIL-A"].UbicacionId,
            UbicacionDestinoId = ubicaciones["TEC"].UbicacionId,
            Observaciones = "Nota nueva",
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task EnvioEnMovimiento_NoSePuedeEditar()
    {
        await using var db = CrearContexto();
        var (envio, ubicaciones) = await SembrarAsync(db, EstadoEnvioCodigos.EntregadoATransportacion, DireccionEnvioEnum.HaciaTecnologia);

        var resultado = await Servicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = ubicaciones["FIL-A"].UbicacionId,
            UbicacionDestinoId = ubicaciones["TEC"].UbicacionId,
            Observaciones = "Ya no debería poder",
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    [Fact]
    public async Task Editar_RegistraEnElHistorialQueSeEdito()
    {
        await using var db = CrearContexto();
        var (envio, ubicaciones) = await SembrarAsync(db, EstadoEnvioCodigos.EnPreparacionTecnologia, DireccionEnvioEnum.HaciaFilial);
        var historialPrevio = await db.HistorialEstadosEnvio.CountAsync();

        await Servicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = ubicaciones["TEC"].UbicacionId,
            UbicacionDestinoId = ubicaciones["FIL-B"].UbicacionId,
            Observaciones = "Destino corregido",
        });

        var registros = await db.HistorialEstadosEnvio
            .Where(x => x.EnvioId == envio.EnvioId)
            .OrderBy(x => x.HistorialEstadoEnvioId)
            .ToListAsync();

        Assert.Equal(historialPrevio + 1, registros.Count);
        Assert.Contains("edit", registros[^1].Observaciones!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UsuarioId, registros[^1].UsuarioId);
    }

    [Fact]
    public async Task Editar_DejaConstanciaDeQueCambio()
    {
        await using var db = CrearContexto();
        var (envio, ubicaciones) = await SembrarAsync(db, EstadoEnvioCodigos.EnPreparacionTecnologia, DireccionEnvioEnum.HaciaFilial);

        await Servicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = ubicaciones["TEC"].UbicacionId,
            UbicacionDestinoId = ubicaciones["FIL-B"].UbicacionId,
            Observaciones = "Destino corregido",
        });

        var ultimo = await db.HistorialEstadosEnvio
            .Where(x => x.EnvioId == envio.EnvioId)
            .OrderByDescending(x => x.HistorialEstadoEnvioId)
            .FirstAsync();

        // El registro nombra la filial nueva: sin eso el historial diría "se editó" sin decir qué.
        Assert.Contains("Filial B", ultimo.Observaciones!);
    }

    [Fact]
    public async Task EditarSinCambios_NoEnsuciaElHistorial()
    {
        await using var db = CrearContexto();
        var (envio, ubicaciones) = await SembrarAsync(db, EstadoEnvioCodigos.EnPreparacionTecnologia, DireccionEnvioEnum.HaciaFilial);
        var historialPrevio = await db.HistorialEstadosEnvio.CountAsync();

        await Servicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = envio.UbicacionOrigenId,
            UbicacionDestinoId = envio.UbicacionDestinoId,
            Observaciones = envio.Observaciones,
        });

        Assert.Equal(historialPrevio, await db.HistorialEstadosEnvio.CountAsync());
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var usuario = FakeUserContext.Global(UsuarioId);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario), new CasoEquipoService(db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario)));
    }

    private static async Task<(Envio, Dictionary<string, Ubicacion>)> SembrarAsync(
        SistemaEnviosDbContext db, string codigoEstado, DireccionEnvioEnum direccion)
    {
        var ubicaciones = new Dictionary<string, Ubicacion>
        {
            ["TEC"] = new() { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true },
            ["FIL-A"] = new() { Nombre = "Filial A", CodigoCentro = "FIL-A", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true },
            ["FIL-B"] = new() { Nombre = "Filial B", CodigoCentro = "FIL-B", FilialExternaId = 2, Tipo = TipoUbicacionEnum.Filial, Activo = true },
        };
        var estados = new[] { EstadoEnvioCodigos.EnFilial, EstadoEnvioCodigos.EnPreparacionTecnologia, EstadoEnvioCodigos.EntregadoATransportacion }
            .ToDictionary(x => x, x => new EstadoEnvio { Codigo = x, Nombre = x, Activo = true });
        db.AddRange(ubicaciones.Values);
        db.AddRange(estados.Values);
        await db.SaveChangesAsync();

        var esHaciaFilial = direccion == DireccionEnvioEnum.HaciaFilial;
        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-EDIT001",
            UbicacionOrigenId = esHaciaFilial ? ubicaciones["TEC"].UbicacionId : ubicaciones["FIL-A"].UbicacionId,
            UbicacionDestinoId = esHaciaFilial ? ubicaciones["FIL-A"].UbicacionId : ubicaciones["TEC"].UbicacionId,
            EstadoEnvioId = estados[codigoEstado].EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Original",
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();
        return (envio, ubicaciones);
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
