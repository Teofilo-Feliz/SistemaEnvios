using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Casos;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Casos;

/// <summary>
/// Un caso es la vida de un equipo fuera de su filial por un mismo asunto: abre cuando el
/// equipo sale y cierra cuando esa misma filial lo recibe conforme, o cuando lo descarta.
/// Mientras siga abierto, todo movimiento del equipo hereda su ticket.
///
/// El cierre se sella en la fila de apertura en el momento en que ocurre. La alternativa era
/// deducirlo en cada consulta a partir de recepciones y direcciones, y entonces "¿este equipo
/// tiene caso abierto?" — que se pregunta en cada envío — dependería de reconstruir su historia.
/// </summary>
public sealed class CasoEquipoService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IAlcanceEnvios alcance) : ICasoEquipoService
{
    public async Task<CasoAbiertoResponse?> BuscarAbiertoAsync(int equipoId, CancellationToken cancellationToken = default)
    {
        var apertura = await db.EnvioEquipos.AsNoTracking()
            .Where(x => x.EquipoId == equipoId
                        && x.EnvioEquipoOrigenId == null
                        && x.FechaCierreCaso == null)
            .OrderByDescending(x => x.EnvioEquipoId)
            .Select(x => new
            {
                x.EnvioEquipoId,
                x.NumeroTicket,
                x.FechaCreacion,
                // La filial del caso es la que participa en la apertura: el origen cuando el
                // equipo sale hacia Tecnología, el destino cuando es una asignación inicial.
                FilialId = x.Envio.Direccion == DireccionEnvioEnum.HaciaTecnologia
                    ? x.Envio.UbicacionOrigenId
                    : x.Envio.UbicacionDestinoId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (apertura is null) return null;

        var movimientos = await db.EnvioEquipos.AsNoTracking()
            .CountAsync(x => x.EnvioEquipoId == apertura.EnvioEquipoId
                             || x.EnvioEquipoOrigenId == apertura.EnvioEquipoId, cancellationToken);

        return new CasoAbiertoResponse(
            apertura.EnvioEquipoId, apertura.NumeroTicket, apertura.FilialId, movimientos, apertura.FechaCreacion);
    }

    public async Task<Result<CasoAbiertoResponse?>> ConsultarDeEquipoAsync(
        int equipoId, CancellationToken cancellationToken = default)
    {
        var alcanzable = await (await alcance.FiltrarEquiposAsync(db.Equipos.AsNoTracking(), cancellationToken))
            .AnyAsync(x => x.EquipoId == equipoId, cancellationToken);
        if (!alcanzable)
            return Result<CasoAbiertoResponse?>.Failure(
                "El equipo no está dentro de su alcance.", ErrorType.Forbidden);

        return Result<CasoAbiertoResponse?>.Success(await BuscarAbiertoAsync(equipoId, cancellationToken));
    }

    /// <summary>
    /// Sella el cierre en la apertura. Es idempotente a propósito: la recepción conforme y un
    /// descarte podrían llegar en cualquier orden, y el primer motivo es el que vale.
    /// </summary>
    public async Task<Result> CerrarAsync(
        int envioEquipoAperturaId, string motivo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var apertura = await db.EnvioEquipos
            .FirstOrDefaultAsync(x => x.EnvioEquipoId == envioEquipoAperturaId, cancellationToken);
        if (apertura is null) return Result.Failure("El movimiento de apertura no existe.", ErrorType.NotFound);
        if (apertura.EnvioEquipoOrigenId is not null)
            return Result.Failure("Solo el movimiento que abrió el caso puede cerrarlo.", ErrorType.Validation);
        if (apertura.FechaCierreCaso is not null) return Result.Success();

        apertura.FechaCierreCaso = DateTime.UtcNow;
        apertura.MotivoCierreCaso = motivo;
        apertura.FechaModificacion = DateTime.UtcNow;
        apertura.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Los casos abiertos cuyo equipo está físicamente en Tecnología. Son los únicos que se
    /// pueden descartar: el descarte decide que el equipo no vuelve a su filial, y eso solo
    /// tiene sentido mientras Tecnología lo tenga en la mano.
    /// </summary>
    public async Task<Result<PaginaResponse<CasoListadoResponse>>> ListarAbiertosEnTecnologiaAsync(
        ConsultarCasosRequest request, CancellationToken cancellationToken = default)
    {
        if (await alcance.ResolverPerfilAsync(cancellationToken) != PerfilAlcance.Global)
            return Result<PaginaResponse<CasoListadoResponse>>.Failure(
                "Solo Tecnología puede consultar los casos abiertos para descarte.", ErrorType.Forbidden);

        var query = db.EnvioEquipos.AsNoTracking()
            .Where(x => x.EnvioEquipoOrigenId == null
                        && x.FechaCierreCaso == null
                        && x.Equipo.UbicacionActual.Tipo == TipoUbicacionEnum.Tecnologia);

        if (request.FilialId.HasValue)
            query = query.Where(x => (x.Envio.Direccion == DireccionEnvioEnum.HaciaTecnologia
                ? x.Envio.UbicacionOrigenId
                : x.Envio.UbicacionDestinoId) == request.FilialId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var termino = request.Search.Trim();
            query = query.Where(x =>
                x.NumeroTicket.Contains(termino) ||
                (x.Equipo.NumeroSerie != null && x.Equipo.NumeroSerie.Contains(termino)) ||
                (x.Equipo.CodigoActivo != null && x.Equipo.CodigoActivo.Contains(termino)));
        }

        var ahora = DateTime.UtcNow;
        var pagina = await query
            .OrderBy(x => x.FechaCreacion).ThenBy(x => x.EnvioEquipoId)
            .PaginarAsync(request, x => new CasoListadoResponse(
                x.EquipoId,
                x.Equipo.NumeroSerie,
                x.Equipo.CodigoActivo,
                x.Equipo.Marca,
                x.Equipo.Modelo,
                x.NumeroTicket,
                x.Envio.Direccion == DireccionEnvioEnum.HaciaTecnologia ? x.Envio.UbicacionOrigenId : x.Envio.UbicacionDestinoId,
                x.Envio.Direccion == DireccionEnvioEnum.HaciaTecnologia ? x.Envio.UbicacionOrigen.Nombre : x.Envio.UbicacionDestino.Nombre,
                db.EnvioEquipos.Count(y => y.EnvioEquipoId == x.EnvioEquipoId || y.EnvioEquipoOrigenId == x.EnvioEquipoId),
                x.FechaCreacion,
                EF.Functions.DateDiffDay(x.FechaCreacion, ahora)), cancellationToken);

        return Result<PaginaResponse<CasoListadoResponse>>.Success(pagina);
    }

    /// <summary>
    /// Descarte: la filial deja de ser dueña del equipo. Cierra el caso y deja el equipo libre
    /// para una asignación inicial nueva, que es la única vía para mandarlo a otra filial.
    /// </summary>
    public async Task<Result> DescartarAsync(
        int equipoId, string motivo, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Failure("El descarte requiere un motivo.", ErrorType.Validation);
        if (await alcance.ResolverPerfilAsync(cancellationToken) != PerfilAlcance.Global)
            return Result.Failure("Solo Tecnología puede descartar un equipo de una filial.", ErrorType.Forbidden);

        var caso = await BuscarAbiertoAsync(equipoId, cancellationToken);
        if (caso is null)
            return Result.Failure("El equipo no tiene un caso abierto que descartar.", ErrorType.Conflict);

        if (await db.ReservasEquipoEnvio.AnyAsync(x => x.EquipoId == equipoId, cancellationToken))
            return Result.Failure(
                "El equipo está reservado en un envío activo. Resuelva ese envío antes de descartarlo.",
                ErrorType.Conflict);

        return await CerrarAsync(caso.EnvioEquipoAperturaId, $"Descartado de la filial: {motivo.Trim()}", usuarioId, cancellationToken);
    }
}
