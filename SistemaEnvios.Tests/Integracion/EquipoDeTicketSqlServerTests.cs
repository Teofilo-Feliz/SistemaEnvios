using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Infrastructure.Integrations.Glpi;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Las consultas de EquipoDeTicketGlpi contra el motor real.
///
/// El proveedor InMemory ejecuta cualquier LINQ en memoria, así que una consulta que SQL Server no
/// sepa traducir pasa verde en las pruebas unitarias y revienta en QA. La del equipo reservado
/// lleva un Any anidado sobre otro DbSet, que es justo la forma que puede no traducirse.
///
/// Solo lee. Se activa con SISTEMAENVIOS_TEST_SQL, como las demás.
/// </summary>
public sealed class EquipoDeTicketSqlServerTests
{
    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    [SkippableFact]
    public async Task LasConsultasDelAutocompletadoSeTraducenASql()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");

        await using var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);

        var servicio = new EquipoDeTicketGlpi(
            new GlpiConUnEquipo(), db, NullLogger<EquipoDeTicketGlpi>.Instance);

        // Un ticket que no está en la base: pasa la comprobación de ticket libre, llega al mapeo y
        // ejecuta contra SQL Server tanto la búsqueda del tipo como la del equipo reservado.
        var resultado = await servicio.ObtenerAsync("987654");

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.NotNull(resultado.Value!.Equipo);
    }

    private sealed class GlpiConUnEquipo : IGlpiClient
    {
        public Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default) =>
            Task.FromResult(Result<TicketGlpi>.Success(
                new TicketGlpi(true, [new ItemDeTicketGlpi("Computer", 657)], EstadoTicketGlpi.EnCurso)));

        public Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default) =>
            Task.FromResult(Result<EquipoGlpi?>.Success(new EquipoGlpi(
                "Computer", "Hewlett-Packard", "HP Compaq 8000", "MXL04916TL",
                null, "LAB-INFORMATICA", "Low Profile Desktop", false)));
    }
}
