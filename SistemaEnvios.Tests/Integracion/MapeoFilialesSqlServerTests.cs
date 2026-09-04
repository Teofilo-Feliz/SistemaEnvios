using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// El id de filial que emite AuthManager en el claim "affiliate" es la llave de todo el
/// alcance. Si una filial lo tiene mal, su gente entra pero no puede hacer nada, y el fallo no
/// aparece hasta que alguien de esa filial intenta trabajar. Ya pasó con Santo Domingo Este:
/// su id se había deducido por eliminación y era falso.
///
/// Estas comprobaciones no adivinan cuál es el id correcto —eso solo lo dice AuthManager— pero
/// sí atrapan los errores estructurales que sí se pueden ver desde aquí.
/// </summary>
public sealed class MapeoFilialesSqlServerTests
{
    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    [SkippableFact]
    public async Task TodaFilialTieneSuIdDeAuthManager()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var sinMapear = await db.Ubicaciones.AsNoTracking()
            .Where(x => x.Tipo == TipoUbicacionEnum.Filial && x.Activo && x.FilialExternaId == null)
            .Select(x => x.Nombre)
            .ToListAsync();

        Assert.True(sinMapear.Count == 0,
            "Estas filiales no tienen id de AuthManager, así que su gente no podrá trabajar: " + string.Join(", ", sinMapear));
    }

    [SkippableFact]
    public async Task NingunIdSeRepiteEntreFiliales()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var repetidos = await db.Ubicaciones.AsNoTracking()
            .Where(x => x.FilialExternaId != null)
            .GroupBy(x => x.FilialExternaId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        // Dos filiales con el mismo id se verían los envíos la una a la otra.
        Assert.True(repetidos.Count == 0, "Ids repetidos entre filiales: " + string.Join(", ", repetidos));
    }

    [SkippableFact]
    public async Task TecnologiaNoLlevaIdDeFilial()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var tecnologia = await db.Ubicaciones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Tipo == TipoUbicacionEnum.Tecnologia);

        Skip.If(tecnologia is null, "No hay ubicación de Tecnología en esta base.");
        // Con id de filial, cualquier empleado de la sede con perfil de filial vería todos los
        // envíos, porque todos pasan por Tecnología.
        Assert.Null(tecnologia!.FilialExternaId);
    }

    private static SistemaEnviosDbContext Contexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);
}
