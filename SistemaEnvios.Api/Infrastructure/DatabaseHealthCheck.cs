using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Api.Infrastructure;
public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SistemaEnviosDbContext>();
            return await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy("SQL Server disponible.") : HealthCheckResult.Unhealthy("SQL Server no disponible.");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Error conectando con SQL Server.", ex); }
    }
}
