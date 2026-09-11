using FluentValidation;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
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
    IUserContext userContext,
    IAlcanceEnvios alcance,
    ICasoEquipoService casos,
    IValidadorTicketGlpi ticketsGlpi) : IEnvioEquipoService
{
    /// <summary>Índice único que sostiene la regla de un ticket por caso.</summary>
    private const string IndiceTicketApertura = "UX_EnvioEquipos_TicketApertura";

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

        var enAlcanceEnvio = await alcance.VerificarAsync(envio.EnvioId, cancellationToken);
        if (enAlcanceEnvio.IsFailure) return Result<int>.Failure(enAlcanceEnvio.Error!, enAlcanceEnvio.ErrorType);

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

        var caso = await casos.BuscarAbiertoAsync(request.EquipoId, cancellationToken);
        if (caso is not null && envio.Direccion == DireccionEnvioEnum.HaciaFilial && envio.UbicacionDestinoId != caso.FilialId)
            return Result<int>.Failure(
                "El equipo tiene un caso abierto con otra filial y debe volver a ella. Descártelo de esa filial para poder reasignarlo.",
                ErrorType.Conflict);

        // Un ticket solo se estrena una vez, y solo las aperturas lo estrenan: las
        // continuaciones repiten el del caso a propósito.
        var numeroTicket = caso?.NumeroTicket ?? request.NumeroTicket.Trim();
        if (caso is null && await db.EnvioEquipos.AnyAsync(
                x => x.EnvioEquipoOrigenId == null && x.NumeroTicket == numeroTicket, cancellationToken))
            return Result<int>.Failure("El número de ticket ya fue utilizado para abrir otro caso.", ErrorType.Conflict);

        // Solo se consulta a GLPI el ticket que escribió el usuario; el heredado ya viene del caso.
        if (caso is null)
        {
            var enGlpi = await ticketsGlpi.ValidarAsync(numeroTicket, cancellationToken);
            if (enGlpi.IsFailure) return Result<int>.Failure(enGlpi.Error!, enGlpi.ErrorType);
        }

        var perteneceAEnvioActivo = await db.ReservasEquipoEnvio.AnyAsync(
            x => x.EquipoId == request.EquipoId,
            cancellationToken);

        if (perteneceAEnvioActivo)
            return Result<int>.Failure("El equipo ya pertenece a otro envío activo.", ErrorType.Conflict);

        var envioEquipo = new EnvioEquipo
        {
            EnvioId = request.EnvioId,
            EquipoId = request.EquipoId,
            NumeroTicket = numeroTicket,
            EnvioEquipoOrigenId = caso?.EnvioEquipoAperturaId,
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
        catch (DbUpdateException ex)
        {
            return Result<int>.Failure(MotivoDelChoque(ex, numeroTicket), ErrorType.Conflict);
        }
        return Result<int>.Success(envioEquipo.EnvioEquipoId);
    }

    public async Task<Result<PaginaResponse<EnvioEquipoResponse>>> ListarPorEnvioAsync(
        int envioId,
        ParametrosPaginaSimple request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<PaginaResponse<EnvioEquipoResponse>>.Failure("El envío no existe.", ErrorType.NotFound);

        var enAlcance = await alcance.VerificarAsync(envioId, cancellationToken);
        if (enAlcance.IsFailure) return Result<PaginaResponse<EnvioEquipoResponse>>.Failure(enAlcance.Error!, enAlcance.ErrorType);

        var pagina = await db.EnvioEquipos
            .AsNoTracking()
            .Where(x => x.EnvioId == envioId)
            .OrderBy(x => x.EnvioEquipoId)
            .PaginarAsync(request, x => new EnvioEquipoResponse(x.EnvioEquipoId, x.EnvioId, x.EquipoId, x.NumeroTicket, x.UsuarioSolicitanteId, x.Observaciones, x.EnvioEquipoOrigenId), cancellationToken);

        return Result<PaginaResponse<EnvioEquipoResponse>>.Success(pagina);
    }

    public async Task<Result<bool>> TicketDisponibleAsync(string numeroTicket, int? excluirEnvioEquipoId = null, CancellationToken cancellationToken = default)
    {
        var ticket = numeroTicket?.Trim();
        // Mismas reglas que al guardarlo: antes bastaba con que fueran dígitos, así que "0"
        // salía "disponible" y el front lo daba por bueno hasta que fallaba al crear el envío.
        if (!NumeroTicket.TieneFormato(ticket))
            return Result<bool>.Failure(NumeroTicket.MensajeFormato, ErrorType.Validation);
        if (!NumeroTicket.EsMayorQueCero(ticket))
            return Result<bool>.Failure(NumeroTicket.MensajeMayorQueCero, ErrorType.Validation);
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

        var enAlcance = await alcance.VerificarAsync(detalle.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;
        if (!EsEstadoEditable(detalle.Envio.EstadoEnvio.Codigo))
            return Result.Failure("El envío ya fue despachado y no admite modificaciones.", ErrorType.Conflict);
        // Una continuación conserva el ticket de su caso pase lo que pase, igual que al crearla:
        // el equipo vuelve a la filial con el mismo ticket con que llegó. Solo una apertura tiene
        // un ticket propio que alguien pueda corregir.
        var esApertura = detalle.EnvioEquipoOrigenId is null;
        var ticket = esApertura ? request.NumeroTicket.Trim() : detalle.NumeroTicket;

        // Una continuación conserva el ticket de su caso, pero antes se ignoraba en silencio lo
        // que viniera en la solicitud y se devolvía éxito: el usuario corregía el ticket, veía
        // "guardado" y al recargar seguía igual, sin explicación. Mejor decirlo.
        if (!esApertura && request.NumeroTicket.Trim() != detalle.NumeroTicket)
            return Result.Failure(
                "El ticket de un equipo devuelto es el del caso que lo abrió y no se cambia aquí: " +
                "el equipo vuelve a la filial con el mismo ticket con que llegó.",
                ErrorType.Validation);

        if (esApertura)
        {
            // Solo las aperturas estrenan un ticket, así que la comparación va contra ellas.
            // Contra todas las filas, una continuación chocaría siempre con su propia apertura y
            // ni siquiera se le podrían corregir las observaciones al equipo devuelto.
            if (await db.EnvioEquipos.AnyAsync(
                    x => x.EnvioEquipoOrigenId == null && x.NumeroTicket == ticket && x.EnvioEquipoId != request.EnvioEquipoId,
                    cancellationToken))
                return Result.Failure("El número de ticket ya fue utilizado en otro envío.", ErrorType.Conflict);

            // Editar una apertura es escribir un ticket a mano igual que al crear, pero con una
            // exigencia más: el ticket tiene que ser el de ESTE equipo. Sin eso se le podía poner
            // el ticket de otra máquina y la fila quedaba mintiendo sobre a qué caso pertenece.
            var equipo = await db.Equipos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.EquipoId == detalle.EquipoId, cancellationToken);
            var ticketEnGlpi = await ticketsGlpi.ValidarParaEquipoAsync(
                ticket, equipo?.NumeroSerie, cancellationToken);
            if (ticketEnGlpi.IsFailure) return ticketEnGlpi;
        }

        detalle.NumeroTicket = ticket;
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

        var enAlcance = await alcance.VerificarAsync(detalle.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;
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

    /// <summary>
    /// Dice cuál de las dos reglas se rompió en la carrera entre dos peticiones.
    /// </summary>
    /// <remarks>
    /// Aquí pueden reventar dos índices únicos distintos: PK_ReservasEquipoEnvio, cuando otro
    /// envío se llevó el equipo, y UX_EnvioEquipos_TicketApertura, cuando otro caso se quedó con
    /// el ticket. Antes los dos salían como "el equipo fue reservado por otro envío", así que
    /// quien chocaba por el ticket se iba a buscar el problema al sitio equivocado.
    ///
    /// Se mira el nombre del índice en el mensaje de SQL Server en vez del número de error: 2601
    /// y 2627 dicen que hubo duplicado, pero no cuál.
    /// </remarks>
    private static string MotivoDelChoque(DbUpdateException ex, string numeroTicket)
    {
        var detalle = ex.InnerException?.Message ?? ex.Message;

        if (detalle.Contains(IndiceTicketApertura, StringComparison.OrdinalIgnoreCase))
        {
            return $"El ticket {numeroTicket} fue utilizado para abrir otro caso mientras se " +
                   "procesaba la solicitud. Un ticket solo puede abrir un caso.";
        }

        return "El equipo fue reservado por otro envío mientras se procesaba la solicitud.";
    }

    // Igual que en EnvioService: los equipos se pueden tocar mientras el envío no se mueva.
    private static bool EsEstadoEditable(string codigo) =>
        codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EnPreparacionTecnologia;

    private static void MarcarEnvioModificado(Envio envio, Guid usuarioId)
    {
        envio.FechaModificacion = DateTime.UtcNow;
        envio.UsuarioModificacionId = usuarioId;
    }
}
