using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// Las tablas de acceso se cruzan con textos que emite AuthManager, así que el sistema solo
/// funciona si esos textos coinciden carácter por carácter con los sembrados. No coincidían: el
/// script traía 'Encargado de Transportacion', un nombre que AuthManager nunca emite.
///
/// El encargado de Transportación entraba autenticado, caía al perfil de respaldo (Filial, por
/// tener filial 30) y aterrizaba en /filial, que exige 'envios.consultar'. Como no lo tenía, el
/// guardián lo mandaba a /unauthorized.
///
/// Estas comprobaciones usan las claves exactas del token real de Ramón Matos.
/// </summary>
public sealed class AccesoEncargadoTransportacionSqlServerTests
{
    // Copiadas del token emitido por AuthManager. El rol trae espacio al final y acento; la
    // posición es el cargo de recursos humanos, escrito sin acentos y con "y mecanica".
    private const string RolDelToken = "Encargado Transportación ";
    private const string PosicionDelToken = "Encargado transportacion y mecanica";
    private const string FilialDelToken = "30,SANTO DOMINGO (SEDE)";

    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    [SkippableFact]
    public async Task ElEncargadoDeTransportacionResuelveASuPerfil()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var perfil = await Alcance(db).ResolverPerfilAsync();

        // Sin mapeo caía en Filial: tiene filial 30 (la sede), así que el respaldo lo daba por
        // administrador de filial. Es el fallo que lo mandaba a /unauthorized.
        Assert.Equal(PerfilAlcance.Transportacion, perfil);
    }

    [SkippableFact]
    public async Task PuedeConsultarEnviosParaQueLeAbraSuModulo()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var permisos = await PermisosDeAsync(db, RolDelToken);

        // El token solo trae transportes.*, y /transportacion exige envios.consultar: sin este
        // permiso el guardián rebota igual, aunque el perfil ya sea el correcto.
        Assert.Contains(PermissionNames.EnviosConsultar, permisos);
    }

    [SkippableFact]
    public async Task ConservaLoQueTransportacionSiHace()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var permisos = await PermisosDeAsync(db, RolDelToken);

        Assert.Contains(PermissionNames.TransportesConfirmar, permisos);
        Assert.Contains(PermissionNames.TransportesGestionar, permisos);
    }

    [SkippableFact]
    public async Task NoLeDaLoQueSoloLeTocaATecnologia()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var permisos = await PermisosDeAsync(db, RolDelToken);

        // Transportación custodia y confirma; no crea envíos ni administra catálogos.
        Assert.DoesNotContain(PermissionNames.EnviosCrear, permisos);
        Assert.DoesNotContain(PermissionNames.CatalogosAdministrar, permisos);
    }

    /// <summary>
    /// El acento importa: la base es Modern_Spanish_CI_AS, que ignora mayúsculas pero distingue
    /// tildes. 'Transportacion' sembrado no cruza con 'Transportación' del token.
    /// </summary>
    [SkippableFact]
    public async Task ElRolSeSembroConElAcentoQueEmiteAuthManager()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var clave = RolDelToken.Trim();
        Assert.True(
            await db.PerfilesPorPosicion.AsNoTracking().AnyAsync(x => x.Posicion == clave),
            $"Falta la fila '{clave}' en PerfilesPorPosicion; sin ella el encargado va a /unauthorized.");
    }

    private static async Task<List<string>> PermisosDeAsync(SistemaEnviosDbContext db, string clave)
    {
        var buscada = clave.Trim();
        return await db.PermisosPorPosicion.AsNoTracking()
            .Where(x => x.Posicion == buscada)
            .Select(x => x.Permiso)
            .ToListAsync();
    }

    // El alcance real contra la base real: el mapeo que se prueba es el que está sembrado, no
    // uno inventado por la prueba.
    private static AlcanceEnvios Alcance(SistemaEnviosDbContext db) =>
        new(db, new FakeUserContext(
            Guid.Parse("78b7c168-eacb-45f1-93f2-a0528f727c7a"),
            FilialDelToken,
            roles: [RolDelToken.Trim()],
            position: PosicionDelToken),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<AlcanceEnvios>.Instance);

    private static SistemaEnviosDbContext Contexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);
}
