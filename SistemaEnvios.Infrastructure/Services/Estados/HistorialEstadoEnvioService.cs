using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class HistorialEstadoEnvioService(SistemaEnviosDbContext db, IAlcanceEnvios alcance) : IHistorialEstadoEnvioService
{
    public async Task<Result<PaginaResponse<HistorialEstadoEnvioResponse>>> ListarPorEnvioAsync(int envioId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default)
    {
        if (!await db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<PaginaResponse<HistorialEstadoEnvioResponse>>.Failure("El envío no existe.", ErrorType.NotFound);

        var enAlcance = await alcance.VerificarAsync(envioId, cancellationToken);
        if (enAlcance.IsFailure) return Result<PaginaResponse<HistorialEstadoEnvioResponse>>.Failure(enAlcance.Error!, enAlcance.ErrorType);

        var pagina = await db.HistorialEstadosEnvio.AsNoTracking().Where(x => x.EnvioId == envioId)
            .OrderBy(x => x.Fecha).ThenBy(x => x.HistorialEstadoEnvioId)
            .PaginarAsync(request, x => new HistorialEstadoEnvioResponse(x.HistorialEstadoEnvioId, x.EnvioId, x.EstadoEnvioId, x.Fecha, x.UbicacionId, x.UsuarioId, x.Observaciones), cancellationToken);
        return Result<PaginaResponse<HistorialEstadoEnvioResponse>>.Success(pagina);
    }
}
