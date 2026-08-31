using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Envios;

public sealed class EnvioEquipoService(
    IEnvioEquipoRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<AgregarEquipoEnvioRequest> validator,
    IValidator<ActualizarEnvioEquipoRequest> actualizarValidator,
    SistemaEnviosDbContext db,
    IUserContext userContext) : IEnvioEquipoService
{
    public async Task<Result<int>> AgregarAsync(
        AgregarEquipoEnvioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);

        if (userContext.UserId is not Guid usuarioId)
            return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var envio = await db.Envios
            .Include(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);

        if (envio is null)
            return Result<int>.Failure("El envío no existe.", ErrorType.NotFound);

        var equipo = await db.Equipos.FindAsync([request.EquipoId], cancellationToken);
        if (equipo is null)
            return Result<int>.Failure("El equipo no existe.", ErrorType.NotFound);

        if (!EsEstadoEditable(envio.EstadoEnvio.Codigo))
            return Result<int>.Failure("El envío ya fue despachado y no admite nuevos equipos.", ErrorType.Conflict);

        if (equipo.UbicacionActualId != envio.UbicacionOrigenId)
            return Result<int>.Failure(
                "El equipo no se encuentra en la ubicación de origen del envío.",
                ErrorType.Conflict);

        if (await repository.ExisteEnEnvioAsync(request.EnvioId, request.EquipoId, cancellationToken))
            return Result<int>.Failure("El equipo ya pertenece a este envío.", ErrorType.Conflict);

        if (await db.EnvioEquipos.AnyAsync(x => x.NumeroTicket == request.NumeroTicket.Trim(), cancellationToken))
            return Result<int>.Failure("El número de ticket ya fue utilizado en otro envío.", ErrorType.Conflict);

        var perteneceAEnvioActivo = await db.ReservasEquipoEnvio.AnyAsync(
            x => x.EquipoId == request.EquipoId,
            cancellationToken);

        if (perteneceAEnvioActivo)
            return Result<int>.Failure("El equipo ya pertenece a otro envío activo.", ErrorType.Conflict);

        var envioEquipo = new EnvioEquipo
        {
            EnvioId = request.EnvioId,
            EquipoId = request.EquipoId,
            NumeroTicket = request.NumeroTicket.Trim(),
            UsuarioSolicitanteId = usuarioId,
            Observaciones = request.Observaciones.Trim(),
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId
        };

        await repository.AgregarAsync(envioEquipo, cancellationToken);
        db.ReservasEquipoEnvio.Add(new ReservaEquipoEnvio
        {
            EquipoId = request.EquipoId,
            EnvioId = request.EnvioId,
            FechaReserva = DateTime.UtcNow,
            UsuarioId = usuarioId
        });
        equipo.FechaModificacion = DateTime.UtcNow;
        equipo.UsuarioModificacionId = usuarioId;
        MarcarEnvioModificado(envio, usuarioId);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<int>.Failure(
                "El equipo fue reservado por otro envío mientras se procesaba la solicitud.",
                ErrorType.Conflict);
        }
        return Result<int>.Success(envioEquipo.EnvioEquipoId);
    }

    public async Task<Result<IReadOnlyCollection<EnvioEquipoResponse>>> ListarPorEnvioAsync(
        int envioId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<IReadOnlyCollection<EnvioEquipoResponse>>.Failure("El envío no existe.", ErrorType.NotFound);

        var equipos = await db.EnvioEquipos
            .AsNoTracking()
            .Where(x => x.EnvioId == envioId)
            .OrderBy(x => x.EnvioEquipoId)
            .Select(x => new EnvioEquipoResponse(x.EnvioEquipoId, x.EnvioId, x.EquipoId, x.NumeroTicket, x.UsuarioSolicitanteId, x.Observaciones))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<EnvioEquipoResponse>>.Success(equipos);
    }

    public async Task<Result<bool>> TicketDisponibleAsync(string numeroTicket, int? excluirEnvioEquipoId = null, CancellationToken cancellationToken = default)
    {
        var ticket = numeroTicket?.Trim();
        if (string.IsNullOrWhiteSpace(ticket) || !ticket.All(char.IsDigit))
            return Result<bool>.Failure("El número de ticket debe contener únicamente caracteres numéricos.", ErrorType.Validation);
        var existe = await db.EnvioEquipos.AsNoTracking().AnyAsync(
            x => x.NumeroTicket == ticket && (!excluirEnvioEquipoId.HasValue || x.EnvioEquipoId != excluirEnvioEquipoId.Value),
            cancellationToken);
        return Result<bool>.Success(!existe);
    }

    public async Task<Result> ActualizarAsync(
        ActualizarEnvioEquipoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await actualizarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var detalle = await db.EnvioEquipos.Include(x => x.Envio).ThenInclude(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioEquipoId == request.EnvioEquipoId, cancellationToken);
        if (detalle is null)
            return Result.Failure("El equipo asociado al envío no existe.", ErrorType.NotFound);
        if (!EsEstadoEditable(detalle.Envio.EstadoEnvio.Codigo))
            return Result.Failure("El envío ya fue despachado y no admite modificaciones.", ErrorType.Conflict);
        if (await db.EnvioEquipos.AnyAsync(x => x.NumeroTicket == request.NumeroTicket.Trim() && x.EnvioEquipoId != request.EnvioEquipoId, cancellationToken))
            return Result.Failure("El número de ticket ya fue utilizado en otro envío.", ErrorType.Conflict);

        detalle.NumeroTicket = request.NumeroTicket.Trim();
        detalle.Observaciones = request.Observaciones.Trim();
        detalle.FechaModificacion = DateTime.UtcNow;
        detalle.UsuarioModificacionId = usuarioId;
        MarcarEnvioModificado(detalle.Envio, usuarioId);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.", ErrorType.Conflict);
        }
        return Result.Success();
    }

    public async Task<Result> QuitarAsync(int envioEquipoId, CancellationToken cancellationToken = default)
    {
        if (envioEquipoId <= 0)
            return Result.Failure("El identificador es requerido.", ErrorType.Validation);
        if (userContext.UserId is null)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var detalle = await db.EnvioEquipos.Include(x => x.Envio).ThenInclude(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioEquipoId == envioEquipoId, cancellationToken);
        if (detalle is null)
            return Result.Failure("El equipo asociado al envío no existe.", ErrorType.NotFound);
        if (!EsEstadoEditable(detalle.Envio.EstadoEnvio.Codigo))
            return Result.Failure("El envío ya fue despachado y no admite modificaciones.", ErrorType.Conflict);

        var reserva = await db.ReservasEquipoEnvio.FindAsync([detalle.EquipoId], cancellationToken);
        if (reserva is not null)
            db.ReservasEquipoEnvio.Remove(reserva);
        db.EnvioEquipos.Remove(detalle);
        MarcarEnvioModificado(detalle.Envio, userContext.UserId!.Value);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.", ErrorType.Conflict);
        }
        return Result.Success();
    }

    private static bool EsEstadoEditable(string codigo) =>
        codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EnPreparacionTecnologia;

    private static void MarcarEnvioModificado(Envio envio, Guid usuarioId)
    {
        envio.FechaModificacion = DateTime.UtcNow;
        envio.UsuarioModificacionId = usuarioId;
    }
}
