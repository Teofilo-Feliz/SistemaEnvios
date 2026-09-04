using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Recepciones;

public sealed class RecepcionService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CrearRecepcionRequest> crearValidator,
    IValidator<VerificarEquipoRequest> verificarValidator,
    IValidator<AsignarTecnicoRequest> asignarValidator,
    IUserContext userContext,
    IAlcanceEnvios alcance,
    ICasoEquipoService casos) : IRecepcionService
{
    public async Task<Result<PaginaResponse<IncidenciaRecepcionResponse>>> ListarIncidenciasPorEnvioAsync(
        int envioId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default)
    {
        var enAlcance = await alcance.VerificarAsync(envioId, cancellationToken);
        if (enAlcance.IsFailure)
            return Result<PaginaResponse<IncidenciaRecepcionResponse>>.Failure(enAlcance.Error!, enAlcance.ErrorType);

        var pagina = await db.RecepcionEquipos.AsNoTracking()
            .Where(x => x.Recepcion.EnvioId == envioId
                        && x.EstadoRecepcionEquipo == EstadoRecepcionEquipoEnum.VerificadoConIncidencia)
            .OrderBy(x => x.RecepcionEquipoId)
            .PaginarAsync(request, x => new IncidenciaRecepcionResponse(
                x.EnvioEquipoId,
                x.EnvioEquipo.EquipoId,
                x.EnvioEquipo.Equipo.NumeroSerie,
                x.EnvioEquipo.Equipo.CodigoActivo,
                x.EnvioEquipo.Equipo.Marca,
                x.EnvioEquipo.Equipo.Modelo,
                x.EnvioEquipo.NumeroTicket,
                x.Observaciones,
                x.FechaVerificacion), cancellationToken);

        return Result<PaginaResponse<IncidenciaRecepcionResponse>>.Success(pagina);
    }

    public async Task<Result<RecepcionResponse>> ObtenerPorEnvioAsync(
        int envioId,
        CancellationToken cancellationToken = default)
    {
        var enAlcance = await alcance.VerificarAsync(envioId, cancellationToken);
        if (enAlcance.IsFailure) return Result<RecepcionResponse>.Failure(enAlcance.Error!, enAlcance.ErrorType);

        var recepcion = await db.Recepciones
            .AsNoTracking()
            .Where(x => x.EnvioId == envioId)
            .Select(x => new RecepcionResponse(x.RecepcionId, x.EnvioId, x.TecnicoAsignadoUsuarioId, x.TecnicoAsignadoNombre, x.TecnicoAsignadoNumeroEmpleado,
                x.UsuarioQueRecibioId, x.FechaAsignacion, x.FechaRecepcion, x.EstadoRecepcion,
                x.Observaciones, x.UsuarioQueAsignoId))
            .FirstOrDefaultAsync(cancellationToken);

        return recepcion is null
            ? Result<RecepcionResponse>.Failure("La recepción no existe.", ErrorType.NotFound)
            : Result<RecepcionResponse>.Success(recepcion);
    }

    public async Task<Result<int>> CrearAsync(
        CrearRecepcionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await crearValidator.ValidateAsync(request, cancellationToken);
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

        if (!PermiteCrearRecepcion(envio.Direccion, envio.EstadoEnvio.Codigo))
            return Result<int>.Failure("El estado actual del envío no permite iniciar la recepción.", ErrorType.Conflict);

        if (await db.Recepciones.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken))
            return Result<int>.Failure("El envío ya tiene una recepción.", ErrorType.Conflict);

        UsuarioReferencia? tecnico = null;
        if (request.TecnicoAsignadoUsuarioId.HasValue)
        {
            tecnico = await db.UsuariosReferencia.FirstOrDefaultAsync(x => x.UsuarioExternoId == request.TecnicoAsignadoUsuarioId && x.EsTecnico && x.Activo, cancellationToken);
            if (tecnico is null) return Result<int>.Failure("El técnico no existe, está inactivo o no está sincronizado desde AuthManager.", ErrorType.Validation);
        }
        var fechaActual = DateTime.UtcNow;
        var recepcion = new Recepcion
        {
            EnvioId = request.EnvioId,
            UsuarioQueAsignoId = usuarioId,
            TecnicoAsignadoUsuarioId = tecnico?.UsuarioExternoId,
            TecnicoAsignadoNombre = tecnico?.NombreCompleto,
            TecnicoAsignadoNumeroEmpleado = tecnico?.NumeroEmpleado,
            FechaAsignacion = tecnico is not null ? fechaActual : null,
            EstadoRecepcion = tecnico is not null
                ? EstadoRecepcionEnum.Asignada
                : EstadoRecepcionEnum.Pendiente,
            Observaciones = NormalizarOpcional(request.Observaciones),
            FechaCreacion = fechaActual,
            UsuarioCreacionId = usuarioId
        };

        db.Recepciones.Add(recepcion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(recepcion.RecepcionId);
    }

    public async Task<Result> VerificarEquipoAsync(
        VerificarEquipoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await verificarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);

        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var recepcion = await db.Recepciones
            .Include(x => x.Envio)
            .ThenInclude(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.RecepcionId == request.RecepcionId, cancellationToken);

        if (recepcion is null)
            return Result.Failure("La recepción no existe.", ErrorType.NotFound);

        var enAlcance = await alcance.VerificarAsync(recepcion.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;

        if (recepcion.EstadoRecepcion is EstadoRecepcionEnum.Completada or EstadoRecepcionEnum.CompletadaConIncidencia)
            return Result.Failure("La recepción ya fue completada.", ErrorType.Conflict);

        if (!PermiteVerificar(recepcion.Envio.Direccion, recepcion.Envio.EstadoEnvio.Codigo))
            return Result.Failure("El estado actual del envío no permite verificar equipos.", ErrorType.Conflict);

        var equipoValido = await db.EnvioEquipos.AnyAsync(
            x => x.EnvioEquipoId == request.EnvioEquipoId && x.EnvioId == recepcion.EnvioId,
            cancellationToken);
        if (!equipoValido)
            return Result.Failure("El equipo no pertenece al envío.", ErrorType.Validation);

        if (await db.RecepcionEquipos.AnyAsync(x => x.EnvioEquipoId == request.EnvioEquipoId, cancellationToken))
            return Result.Failure("El equipo ya fue verificado.", ErrorType.Conflict);

        var fechaActual = DateTime.UtcNow;
        db.RecepcionEquipos.Add(new RecepcionEquipo
        {
            RecepcionId = request.RecepcionId,
            EnvioEquipoId = request.EnvioEquipoId,
            EstadoRecepcionEquipo = request.Estado,
            FechaVerificacion = fechaActual,
            Observaciones = NormalizarOpcional(request.Observaciones),
            FechaCreacion = fechaActual,
            UsuarioCreacionId = usuarioId
        });

        recepcion.EstadoRecepcion = EstadoRecepcionEnum.EnProceso;
        recepcion.FechaModificacion = fechaActual;
        recepcion.UsuarioModificacionId = usuarioId;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AsignarTecnicoAsync(
        AsignarTecnicoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await asignarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var recepcion = await db.Recepciones.FindAsync([request.RecepcionId], cancellationToken);
        if (recepcion is null)
            return Result.Failure("La recepción no existe.", ErrorType.NotFound);

        var enAlcance = await alcance.VerificarAsync(recepcion.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;
        if (recepcion.EstadoRecepcion is EstadoRecepcionEnum.EnProceso or
            EstadoRecepcionEnum.Completada or
            EstadoRecepcionEnum.CompletadaConIncidencia)
            return Result.Failure("La recepción ya inició y no admite reasignación.", ErrorType.Conflict);

        var fecha = DateTime.UtcNow;
        var tecnico = await db.UsuariosReferencia.FirstOrDefaultAsync(x => x.UsuarioExternoId == request.TecnicoAsignadoUsuarioId && x.EsTecnico && x.Activo, cancellationToken);
        if (tecnico is null) return Result.Failure("El técnico no existe, está inactivo o no está sincronizado desde AuthManager.", ErrorType.Validation);
        recepcion.TecnicoAsignadoUsuarioId = tecnico.UsuarioExternoId;
        recepcion.TecnicoAsignadoNombre = tecnico.NombreCompleto;
        recepcion.TecnicoAsignadoNumeroEmpleado = tecnico.NumeroEmpleado;
        recepcion.FechaAsignacion = fecha;
        recepcion.EstadoRecepcion = EstadoRecepcionEnum.Asignada;
        recepcion.FechaModificacion = fecha;
        recepcion.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CompletarAsync(
        int recepcionId,
        CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var recepcion = await db.Recepciones
            .Include(x => x.Envio)
            .ThenInclude(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.RecepcionId == recepcionId, cancellationToken);

        if (recepcion is null)
            return Result.Failure("La recepción no existe.", ErrorType.NotFound);

        var enAlcance = await alcance.VerificarAsync(recepcion.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;

        if (recepcion.EstadoRecepcion is EstadoRecepcionEnum.Completada or EstadoRecepcionEnum.CompletadaConIncidencia)
            return Result.Failure("La recepción ya fue completada.", ErrorType.Conflict);

        var totalEquipos = await db.EnvioEquipos.CountAsync(
            x => x.EnvioId == recepcion.EnvioId,
            cancellationToken);
        if (totalEquipos == 0)
            return Result.Failure("El envío no tiene equipos registrados.", ErrorType.Conflict);

        var equiposVerificados = await db.RecepcionEquipos.CountAsync(
            x => x.RecepcionId == recepcionId,
            cancellationToken);
        if (equiposVerificados != totalEquipos)
            return Result.Failure("Aún existen equipos pendientes de verificación.", ErrorType.Conflict);

        var conIncidencia = await db.RecepcionEquipos.AnyAsync(
            x => x.RecepcionId == recepcionId &&
                 x.EstadoRecepcionEquipo == EstadoRecepcionEquipoEnum.VerificadoConIncidencia,
            cancellationToken);
        // El estado dice cómo llegó. Marcarlo solo en la recepción obligaba a abrirla para
        // enterarse de que algo vino mal, y quien mira la lista necesita verlo ahí.
        var codigoFinal = recepcion.Envio.Direccion == DireccionEnvioEnum.HaciaTecnologia
            ? (conIncidencia ? EstadoEnvioCodigos.RecibidoPorTecnologiaConIncidencia : EstadoEnvioCodigos.RecibidoPorTecnologia)
            : (conIncidencia ? EstadoEnvioCodigos.RecibidoEnFilialConIncidencia : EstadoEnvioCodigos.RecibidoEnFilial);
        var estadoFinal = await db.EstadosEnvio.FirstOrDefaultAsync(
            x => x.Codigo == codigoFinal && x.Activo && x.EsFinal,
            cancellationToken);

        if (estadoFinal is null)
            return Result.Failure("El estado final del flujo no se encuentra configurado.", ErrorType.Conflict);

        var transicionPermitida = await db.TransicionesEstadoEnvio.AnyAsync(
            x => x.EstadoOrigenId == recepcion.Envio.EstadoEnvioId &&
                 x.EstadoDestinoId == estadoFinal.EstadoEnvioId && x.Activo,
            cancellationToken);
        if (!transicionPermitida)
            return Result.Failure("El estado actual del envío no permite completar la recepción.", ErrorType.Conflict);

        var fechaActual = DateTime.UtcNow;
        recepcion.EstadoRecepcion = conIncidencia
            ? EstadoRecepcionEnum.CompletadaConIncidencia
            : EstadoRecepcionEnum.Completada;
        recepcion.UsuarioQueRecibioId = usuarioId;
        recepcion.FechaRecepcion = fechaActual;
        recepcion.FechaModificacion = fechaActual;
        recepcion.UsuarioModificacionId = usuarioId;

        recepcion.Envio.EstadoEnvioId = estadoFinal.EstadoEnvioId;
        recepcion.Envio.FechaFinalizacion = fechaActual;
        recepcion.Envio.FechaModificacion = fechaActual;
        recepcion.Envio.UsuarioModificacionId = usuarioId;

        // El caso se cierra cuando el equipo vuelve a su filial y llega bien. Con incidencia
        // sigue abierto: el asunto no se resolvió y la vuelta siguiente hereda su ticket.
        if (!conIncidencia && recepcion.Envio.Direccion == DireccionEnvioEnum.HaciaFilial)
        {
            var equiposDelEnvio = await db.EnvioEquipos.AsNoTracking()
                .Where(x => x.EnvioId == recepcion.EnvioId)
                .Select(x => x.EquipoId)
                .ToListAsync(cancellationToken);

            foreach (var equipoId in equiposDelEnvio)
            {
                var caso = await casos.BuscarAbiertoAsync(equipoId, cancellationToken);
                if (caso is not null)
                    await casos.CerrarAsync(
                        caso.EnvioEquipoAperturaId, "Recibido conforme en la filial.", usuarioId, cancellationToken);
            }
        }

        db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
        {
            EnvioId = recepcion.EnvioId,
            EstadoEnvioId = estadoFinal.EstadoEnvioId,
            UbicacionId = recepcion.Envio.UbicacionDestinoId,
            UsuarioId = usuarioId,
            Fecha = fechaActual,
            Observaciones = conIncidencia
                ? "Recepción completada con incidencia."
                : "Recepción completada.",
            FechaCreacion = fechaActual,
            UsuarioCreacionId = usuarioId
        });

        var equiposRecibidos = await db.EnvioEquipos
            .Where(x => x.EnvioId == recepcion.EnvioId)
            .Select(x => x.Equipo)
            .ToListAsync(cancellationToken);
        foreach (var equipo in equiposRecibidos)
        {
            equipo.UbicacionActualId = recepcion.Envio.UbicacionDestinoId;
            equipo.FechaModificacion = fechaActual;
            equipo.UsuarioModificacionId = usuarioId;
        }

        var reservas = await db.ReservasEquipoEnvio
            .Where(x => x.EnvioId == recepcion.EnvioId)
            .ToListAsync(cancellationToken);
        db.ReservasEquipoEnvio.RemoveRange(reservas);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                "El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.",
                ErrorType.Conflict);
        }
        return Result.Success();
    }

    private static bool PermiteCrearRecepcion(DireccionEnvioEnum direccion, string codigoEstado) =>
        direccion switch
        {
            // Interno: la recepción arranca en RECIBIDO_TRANSPORTACION. Privado: conserva su
            // ruta por ESPERA_TECNOLOGIA y EN_REVISION, que no cambió.
            DireccionEnvioEnum.HaciaTecnologia =>
                codigoEstado is EstadoEnvioCodigos.RecibidoPorTransportacion
                    or EstadoEnvioCodigos.EnEsperaDeTecnologia
                    or EstadoEnvioCodigos.EnProcesoDeRevision,
            // La filial recibe directo desde EN_TRANSITO: no hay un "llegó" separado del
            // "lo recibí", los hacía la misma persona en el mismo momento.
            DireccionEnvioEnum.HaciaFilial =>
                codigoEstado is EstadoEnvioCodigos.EnTransito or EstadoEnvioCodigos.RecibidoEnFilial,
            _ => false
        };

    private static bool PermiteVerificar(DireccionEnvioEnum direccion, string codigoEstado) =>
        direccion switch
        {
            DireccionEnvioEnum.HaciaTecnologia =>
                codigoEstado is EstadoEnvioCodigos.RecibidoPorTransportacion
                    or EstadoEnvioCodigos.EnProcesoDeRevision,
            DireccionEnvioEnum.HaciaFilial => codigoEstado is EstadoEnvioCodigos.EnTransito or EstadoEnvioCodigos.RecibidoEnFilial,
            _ => false
        };

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
