using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class HistorialEstadoEnvioService(SistemaEnviosDbContext db) : IHistorialEstadoEnvioService
{
    public async Task<Result<IReadOnlyCollection<HistorialEstadoEnvio>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        if (!await db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<IReadOnlyCollection<HistorialEstadoEnvio>>.Failure("El envío no existe.");
        var historial = await db.HistorialEstadosEnvio.AsNoTracking()
            .Where(x => x.EnvioId == envioId)
            .OrderBy(x => x.Fecha)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<HistorialEstadoEnvio>>.Success(historial);
    }
}
