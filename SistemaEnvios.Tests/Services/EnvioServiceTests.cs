using FluentValidation;
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

public sealed class EnvioServiceTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8ef22976-c748-4f9b-91b5-67949b524fa2");

    [Fact]
    public async Task CrearDesdeFilial_IniciaFlujoHaciaTecnologiaYRegistraHistorial()
    {
        await using var db = CrearContexto();
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(filial, tecnologia, estado);
        await db.SaveChangesAsync();
        var service = CrearServicio(db);

        var resultado = await service.CrearAsync(new CrearEnvioRequest
        {
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId
        });

        Assert.True(resultado.IsSuccess);
        Assert.Equal(DireccionEnvioEnum.HaciaTecnologia, resultado.Value!.Direccion);
        Assert.Single(await db.HistorialEstadosEnvio.ToListAsync());
        Assert.Equal(UsuarioId, resultado.Value.UsuarioSolicitanteId);
    }

    [Fact]
    public async Task CrearDesdeTecnologia_IniciaFlujoHaciaFilial()
    {
        await using var db = CrearContexto();
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnPreparacionTecnologia, Nombre = "Preparación", Activo = true };
        db.AddRange(tecnologia, filial, estado);
        await db.SaveChangesAsync();
        var service = CrearServicio(db);

        var resultado = await service.CrearAsync(new CrearEnvioRequest
        {
            UbicacionOrigenId = tecnologia.UbicacionId,
            UbicacionDestinoId = filial.UbicacionId
        });

        Assert.True(resultado.IsSuccess);
        Assert.Equal(DireccionEnvioEnum.HaciaFilial, resultado.Value!.Direccion);
        Assert.Equal(estado.EstadoEnvioId, resultado.Value.EstadoEnvioId);
    }

    [Fact]
    public async Task CrearEntreDosFiliales_RechazaLaDireccion()
    {
        await using var db = CrearContexto();
        var origen = new Ubicacion { Nombre = "Filial A", CodigoCentro = "A", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var destino = new Ubicacion { Nombre = "Filial B", CodigoCentro = "B", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        db.AddRange(origen, destino);
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).CrearAsync(new CrearEnvioRequest
        {
            UbicacionOrigenId = origen.UbicacionId,
            UbicacionDestinoId = destino.UbicacionId
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(Application.Common.ErrorType.Validation, resultado.ErrorType);
    }

    [Fact]
    public async Task ActualizarDespuesDelDespacho_RetornaConflicto()
    {
        await using var db = CrearContexto();
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio
        {
            Codigo = EstadoEnvioCodigos.EntregadoATransportacion,
            Nombre = "Entregado a transportación",
            Activo = true
        };
        db.AddRange(filial, tecnologia, estado);
        await db.SaveChangesAsync();
        var envio = new Envio
        {
            NumeroEnvio = "ENV-PRUEBA",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            Observaciones = "Intento de cambio"
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(Application.Common.ErrorType.Conflict, resultado.ErrorType);
    }

    [Fact]
    public async Task CambiarOrigenConEquiposFueraDeLaNuevaUbicacion_RetornaConflicto()
    {
        await using var db = CrearContexto();
        var origenActual = new Ubicacion { Nombre = "Filial A", CodigoCentro = "A", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var nuevoOrigen = new Ubicacion { Nombre = "Filial B", CodigoCentro = "B", Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(origenActual, nuevoOrigen, tecnologia, estado, tipo);
        await db.SaveChangesAsync();
        var equipo = new Equipo
        {
            Marca = "Marca",
            Modelo = "Modelo",
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = origenActual.UbicacionId
        };
        var envio = new Envio
        {
            NumeroEnvio = "ENV-ORIGEN",
            UbicacionOrigenId = origenActual.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.AddRange(equipo, envio);
        await db.SaveChangesAsync();
        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "T-1",
            Observaciones = "Prueba",
            UsuarioSolicitanteId = UsuarioId
        });
        await db.SaveChangesAsync();

        var resultado = await CrearServicio(db).ActualizarAsync(new ActualizarEnvioRequest
        {
            EnvioId = envio.EnvioId,
            UbicacionOrigenId = nuevoOrigen.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(Application.Common.ErrorType.Conflict, resultado.ErrorType);
        Assert.Equal(origenActual.UbicacionId, envio.UbicacionOrigenId);
    }

    private static EnvioService CrearServicio(SistemaEnviosDbContext db) => new(
        new GenericRepository<Envio>(db),
        db,
        new UnitOfWork(db),
        new CrearEnvioRequestValidator(),
        new ActualizarEnvioRequestValidator(),
        FakeUserContext.Global(UsuarioId),
        new AlcanceEnvios(db, FakeUserContext.Global(UsuarioId)),
        new CasoEquipoService(db, new UnitOfWork(db), FakeUserContext.Global(UsuarioId), new AlcanceEnvios(db, FakeUserContext.Global(UsuarioId))));

    private static SistemaEnviosDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SistemaEnviosDbContext(options);
    }
}
