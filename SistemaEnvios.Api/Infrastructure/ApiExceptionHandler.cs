using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SistemaEnvios.Api.Infrastructure;
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var conflict = exception is DbUpdateConcurrencyException or DbUpdateException;
        var status = conflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
        var traceId = context.TraceIdentifier;
        logger.LogError(exception, "Error procesando solicitud. TraceId {TraceId}", traceId);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = conflict ? "Conflicto de concurrencia" : "Error interno",
            Detail = conflict ? "Los datos fueron modificados por otra operación. Actualice e intente nuevamente." : $"Ocurrió un error inesperado. Referencia: {traceId}"
        }, ct);
        return true;
    }
}
