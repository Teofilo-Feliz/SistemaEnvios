using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Todo se guarda en UTC, pero SQL Server devuelve las fechas sin zona (Kind = Unspecified) y
/// entonces el JSON sale sin la "Z". El navegador lee ese texto como hora local, así que un
/// movimiento de las 18:01 aparece a las 22:01: cuatro horas de más, las de Santo Domingo.
///
/// La prueba va contra el motor real a propósito: en memoria las fechas conservan el Kind que
/// se les puso al crearlas, así que el fallo no se reproduce.
/// </summary>
public sealed class FechasUtcSqlServerTests
{
    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    [SkippableFact]
    public async Task LasFechasVuelvenDeLaBaseMarcadasComoUtc()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var fecha = await db.Envios.AsNoTracking()
            .OrderByDescending(x => x.EnvioId)
            .Select(x => (DateTime?)x.FechaCreacion)
            .FirstOrDefaultAsync();

        Skip.If(fecha is null, "No hay envíos en la base para comprobarlo.");
        Assert.Equal(DateTimeKind.Utc, fecha!.Value.Kind);
    }

    [SkippableFact]
    public async Task LaFechaSerializadaLlevaLaZParaQueElNavegadorLaConvierta()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var envio = await db.Envios.AsNoTracking()
            .OrderByDescending(x => x.EnvioId)
            .Select(x => new { x.FechaCreacion })
            .FirstOrDefaultAsync();

        Skip.If(envio is null, "No hay envíos en la base para comprobarlo.");

        // Sin la Z, `new Date(...)` del navegador interpreta el texto como hora local.
        var json = JsonSerializer.Serialize(envio);
        Assert.Contains("Z", json);
    }

    [SkippableFact]
    public async Task ElHistorialTambienVuelveEnUtc()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var fecha = await db.HistorialEstadosEnvio.AsNoTracking()
            .OrderByDescending(x => x.HistorialEstadoEnvioId)
            .Select(x => (DateTime?)x.Fecha)
            .FirstOrDefaultAsync();

        Skip.If(fecha is null, "No hay historial en la base para comprobarlo.");
        Assert.Equal(DateTimeKind.Utc, fecha!.Value.Kind);
    }

    private static SistemaEnviosDbContext Contexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);
}
