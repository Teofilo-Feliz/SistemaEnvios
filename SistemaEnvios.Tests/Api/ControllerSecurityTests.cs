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
                    action.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
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

    [Fact]
    public async Task TodosLosPermisos_EstanRegistradosComoPoliticas()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationAuthorization();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var permission in DefinedPermissions)
            Assert.NotNull(await policyProvider.GetPolicyAsync(permission));
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
