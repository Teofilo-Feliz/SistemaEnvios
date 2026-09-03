using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Notificaciones;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Notificaciones;

public sealed class NotificacionService(SistemaEnviosDbContext db, IUserContext users) : INotificacionService
{
    public async Task<Result<PaginaResponse<NotificacionResponse>>> ListarAsync(ConsultarNotificacionesRequest request, CancellationToken ct = default)
    {
        var q = db.Notificaciones.AsNoTracking().Where(x => x.DestinatarioRol == request.Rol);
        if (request.Rol.Equals("FILIAL", StringComparison.OrdinalIgnoreCase) && users.AffiliateId.HasValue)
            q = q.Where(x => x.Envio.UbicacionDestino.FilialExternaId == users.AffiliateId.Value);

        // El aviso a Tecnología dice "hay un envío esperando en Transportación". En cuanto lo
        // retiran deja de ser cierto, así que desaparece solo. Eso es lo que sustituye al acuse
        // de lectura: el aviso lo cierra el trabajo hecho, no un botón de "ya lo vi".
        if (request.Rol.Equals("TECNOLOGIA", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.Envio.EstadoEnvio.Codigo == EstadoEnvioCodigos.RecibidoPorTransportacion);

        // Antes se cortaba con Take(100) fijo: el usuario no sabía que había más ni podía llegar
        // a ellas. Ahora el corte es una página y el total dice cuántas quedan.
        var pagina = await q.OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.NotificacionId)
            .PaginarAsync(request, x => new NotificacionResponse(x.NotificacionId, x.EnvioId, x.Envio.NumeroEnvio, x.Tipo, x.Titulo, x.Mensaje, x.DestinatarioRol, x.FechaCreacion), ct);
        return Result<PaginaResponse<NotificacionResponse>>.Success(pagina);
    }

}
