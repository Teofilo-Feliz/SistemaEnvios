using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Api.Infrastructure;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;

namespace SistemaEnvios.Tests.Infrastructure;

/// <summary>
/// Defectos de robustez del análisis inicial: el manejador de excepciones que disfrazaba
/// bugs de concurrencia, y el repositorio genérico que adivinaba la clave primaria.
/// </summary>
public sealed class RobustezTests
{
    [Fact]
    public async Task ErrorDeDatos_NoSeDisfrazaDeConflictoDeConcurrencia()
    {
        var contexto = new DefaultHttpContext();
        var manejador = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        // Un NOT NULL o una clave foránea violada es un bug, no una carrera entre usuarios.
        await manejador.TryHandleAsync(contexto, new DbUpdateException("columna nula"), default);

        Assert.Equal(StatusCodes.Status500InternalServerError, contexto.Response.StatusCode);
    }

    [Fact]
    public async Task ConflictoDeConcurrenciaReal_SigueDevolviendo409()
    {
        var contexto = new DefaultHttpContext();
        var manejador = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        await manejador.TryHandleAsync(contexto, new DbUpdateConcurrencyException("ya cambió"), default);

        Assert.Equal(StatusCodes.Status409Conflict, contexto.Response.StatusCode);
    }

    [Fact]
    public async Task RepositorioGenerico_FuncionaConEntidadCuyaLlaveNoSigueLaConvencion()
    {
        await using var db = CrearContexto();
        var equipo = new Equipo { TipoEquipoId = 1, UbicacionActualId = 1, Marca = "M", Modelo = "X" };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();
        db.ReservasEquipoEnvio.Add(new ReservaEquipoEnvio { EquipoId = equipo.EquipoId, EnvioId = 1, UsuarioId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        // La llave de ReservaEquipoEnvio es EquipoId, no "ReservaEquipoEnvioId": con la
        // convención vieja esto reventaba en tiempo de ejecución.
        var repositorio = new GenericRepository<ReservaEquipoEnvio>(db);

        Assert.True(await repositorio.ExistsAsync(equipo.EquipoId));
        Assert.False(await repositorio.ExistsAsync(equipo.EquipoId + 999));
    }

    [Fact]
    public void SoloUnaAccionOmiteLaPolitica_YEsElPerfil()
    {
        // Refuerza la garantía del análisis: la excepción no puede crecer sin decisión.
        var marcadas = typeof(SistemaEnvios.Api.Controllers.Envios.EnviosController).Assembly
            .GetTypes()
            .Where(x => !x.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(x))
            .SelectMany(x => x.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Count(x => x.GetCustomAttribute<SistemaEnvios.Api.Security.AutenticacionSuficienteAttribute>() is not null);

        Assert.Equal(1, marcadas);
    }

    private static SistemaEnviosDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
