using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class HistorialEstadoEnvioService(SistemaEnviosDbContext db) : IHistorialEstadoEnvioService
{
    public async Task<Result<IReadOnlyCollection<HistorialEstadoEnvioResponse>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        if (!await db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<IReadOnlyCollection<HistorialEstadoEnvioResponse>>.Failure("El envío no existe.", ErrorType.NotFound);
        var historial = await db.HistorialEstadosEnvio.AsNoTracking().Where(x => x.EnvioId == envioId)
            .OrderBy(x => x.Fecha)
            .Select(x => new HistorialEstadoEnvioResponse(x.HistorialEstadoEnvioId, x.EnvioId, x.EstadoEnvioId, x.Fecha, x.UbicacionId, x.UsuarioId, x.Observaciones))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<HistorialEstadoEnvioResponse>>.Success(historial);
    }
}
