using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaEnvios.Api.Controllers.Envios;
using SistemaEnvios.Api.Security;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Tests.Api;

public sealed class ControllerSecurityTests
{
    private static readonly IReadOnlySet<string> DefinedPermissions = typeof(PermissionNames)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(x => x.IsLiteral && x.FieldType == typeof(string))
        .Select(x => (string)x.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Permisos que a propósito NO tienen política de autorización: no autorizan una acción, sino
    /// que amplían el alcance. Ningún endpoint los exige; los lee AlcanceEnvios al resolver el
    /// perfil.
    ///
    /// Lista corta y decidida a mano, igual que la de <see cref="SoloElPerfilOmiteLaPolitica"/>:
    /// si aparece otro permiso aquí, hay que justificarlo.
    /// </summary>
    private static readonly IReadOnlySet<string> SinPoliticaAProposito =
        new HashSet<string>(StringComparer.Ordinal) { PermissionNames.AlcanceGlobal };

    [Fact]
    public void TodasLasAcciones_ExigenUnaPoliticaConocida()
    {
        var controllerTypes = typeof(EnviosController).Assembly.GetTypes()
            .Where(x => !x.IsAbstract && typeof(ControllerBase).IsAssignableFrom(x));

        foreach (var controllerType in controllerTypes)
        {
            var controllerPolicies = controllerType.GetCustomAttributes<AuthorizeAttribute>()
                .Select(x => x.Policy);
            var actions = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(x => x.GetCustomAttributes<HttpMethodAttribute>().Any());

            foreach (var action in actions)
            {
                if (controllerType.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                    action.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                    action.GetCustomAttribute<AutenticacionSuficienteAttribute>() is not null)
                    continue;

                var policies = controllerPolicies
                    .Concat(action.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();

                Assert.True(policies.Length > 0, $"{controllerType.Name}.{action.Name} no exige una política.");
                Assert.All(policies, policy => Assert.Contains(policy!, DefinedPermissions));
            }
        }
    }

    /// <summary>
    /// La excepción a "toda acción exige una política" tiene que seguir siendo una lista corta
    /// y decidida a mano. Si aparece otra acción marcada, esta prueba obliga a justificarla.
    /// </summary>
    [Fact]
    public void SoloElPerfilOmiteLaPolitica()
    {
        var marcadas = typeof(EnviosController).Assembly.GetTypes()
            .Where(x => !x.IsAbstract && typeof(ControllerBase).IsAssignableFrom(x))
            .SelectMany(x => x.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(x => x.GetCustomAttribute<AutenticacionSuficienteAttribute>() is not null)
            .Select(x => $"{x.DeclaringType!.Name}.{x.Name}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["PerfilController.Obtener"], marcadas);
    }

    [Fact]
    public async Task TodosLosPermisos_EstanRegistradosComoPoliticas()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationAuthorization();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var permission in DefinedPermissions.Except(SinPoliticaAProposito))
            Assert.NotNull(await policyProvider.GetPolicyAsync(permission));
    }

    /// <summary>
    /// Y al revés: el permiso de alcance NO debe tener política. Si alguien se la agrega, un
    /// endpoint podría exigirlo como si fuera un permiso de acción, y entonces "alcance.global"
    /// significaría dos cosas distintas según dónde se mire.
    /// </summary>
    [Fact]
    public async Task ElPermisoDeAlcanceNoEsUnaPolitica()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationAuthorization();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var permiso in SinPoliticaAProposito)
            Assert.Null(await policyProvider.GetPolicyAsync(permiso));
    }

    [Fact]
    public async Task Politica_AceptaSoloElPermisoSolicitadoDelToken()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationAuthorization();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permissions", $"{PermissionNames.EnviosConsultar},{PermissionNames.EnviosCrear}")],
            "Prueba"));

        var consulta = await authorization.AuthorizeAsync(user, null, PermissionNames.EnviosConsultar);
        var administracion = await authorization.AuthorizeAsync(user, null, PermissionNames.CatalogosAdministrar);

        Assert.True(consulta.Succeeded);
        Assert.False(administracion.Succeeded);
    }
}
