using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class TransicionEstadoEnvioService(
    SistemaEnviosDbContext db) : ITransicionEstadoEnvioService
{
    public async Task<Result<TransicionEstadoResponse>> ObtenerAsync(int transicionId, CancellationToken cancellationToken = default)
    {
        var transicion = await db.TransicionesEstadoEnvio.AsNoTracking().Where(x => x.TransicionEstadoEnvioId == transicionId)
            .Select(x => new TransicionEstadoResponse(x.TransicionEstadoEnvioId, x.EstadoOrigenId, x.EstadoDestinoId, x.Activo))
            .FirstOrDefaultAsync(cancellationToken);
        return transicion is null ? Result<TransicionEstadoResponse>.Failure("La transición no existe.", ErrorType.NotFound) : Result<TransicionEstadoResponse>.Success(transicion);
    }

    public async Task<Result<IReadOnlyCollection<TransicionEstadoResponse>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default)
    {
        var query = db.TransicionesEstadoEnvio.AsNoTracking();
        if (soloActivas) query = query.Where(x => x.Activo);
        var transiciones = await query.OrderBy(x => x.EstadoOrigenId).ThenBy(x => x.EstadoDestinoId)
            .Select(x => new TransicionEstadoResponse(x.TransicionEstadoEnvioId, x.EstadoOrigenId, x.EstadoDestinoId, x.Activo)).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<TransicionEstadoResponse>>.Success(transiciones);
    }

}
