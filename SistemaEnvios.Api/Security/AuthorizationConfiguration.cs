using Microsoft.AspNetCore.Authorization;
using SistemaEnvios.Application.Security;

namespace SistemaEnvios.Api.Security;

public static class AuthorizationConfiguration
{
    private static readonly string[] Permissions =
    [
        PermissionNames.EnviosConsultar,
        PermissionNames.EnviosCrear,
        PermissionNames.EnviosEditar,
        PermissionNames.EnviosDespachar,
        PermissionNames.TransportesGestionar,
        PermissionNames.TransportesConfirmar,
        PermissionNames.RecepcionesGestionar,
        PermissionNames.IncidenciasGestionar,
        PermissionNames.EquiposGestionar,
        PermissionNames.CatalogosAdministrar
    ];

    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            foreach (var permission in Permissions)
            {
                options.AddPolicy(permission, policy =>
                    policy.RequireAssertion(context => HasPermission(context.User, permission)));
            }
        });

        return services;
    }

    private static bool HasPermission(System.Security.Claims.ClaimsPrincipal user, string permission) =>
        user.FindAll("permissions")
            .SelectMany(x => x.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(permission, StringComparer.OrdinalIgnoreCase);
}
