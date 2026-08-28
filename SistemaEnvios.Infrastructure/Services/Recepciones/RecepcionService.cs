using Microsoft.EntityFrameworkCore;
using FluentValidation;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Recepciones;

public sealed class RecepcionService(SistemaEnviosDbContext db, IUnitOfWork unitOfWork, IValidator<CrearRecepcionRequest> crearValidator, IValidator<VerificarEquipoRequest> verificarValidator) : IRecepcionService
{
    public async Task<Result<Recepcion>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        var recepcion = await db.Recepciones.AsNoTracking().FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        return recepcion is null ? Result<Recepcion>.Failure("La recepción no existe.") : Result<Recepcion>.Success(recepcion);
    }

    public async Task<Result<int>> CrearAsync(CrearRecepcionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await crearValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage());
        if (!await db.Envios.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken)) return Result<int>.Failure("El envío no existe.");
        if (await db.Recepciones.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken)) return Result<int>.Failure("El envío ya tiene una recepción.");
        var recepcion = new Recepcion { EnvioId = request.EnvioId, UsuarioQueAsignoId = request.UsuarioQueAsignoId, TecnicoAsignadoId = request.TecnicoAsignadoId, FechaAsignacion = request.TecnicoAsignadoId.HasValue ? DateTime.UtcNow : null, EstadoRecepcion = request.TecnicoAsignadoId.HasValue ? EstadoRecepcionEnum.Asignada : EstadoRecepcionEnum.Pendiente, Observaciones = request.Observaciones, FechaCreacion = DateTime.UtcNow };
        db.Recepciones.Add(recepcion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(recepcion.RecepcionId);
    }

    public async Task<Result> VerificarEquipoAsync(VerificarEquipoRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await verificarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage());
        var recepcion = await db.Recepciones.FindAsync([request.RecepcionId], cancellationToken);
        if (recepcion is null) return Result.Failure("La recepción no existe.");
        if (recepcion.EstadoRecepcion is EstadoRecepcionEnum.Completada or EstadoRecepcionEnum.CompletadaConIncidencia)
            return Result.Failure("La recepción ya fue completada.");
        if (!await db.EnvioEquipos.AnyAsync(x => x.EnvioEquipoId == request.EnvioEquipoId && x.EnvioId == recepcion.EnvioId, cancellationToken)) return Result.Failure("El equipo no pertenece al envío.");
        if (await db.RecepcionEquipos.AnyAsync(x => x.EnvioEquipoId == request.EnvioEquipoId, cancellationToken))
            return Result.Failure("El equipo ya fue verificado.");
        db.RecepcionEquipos.Add(new RecepcionEquipo { RecepcionId = request.RecepcionId, EnvioEquipoId = request.EnvioEquipoId, EstadoRecepcionEquipo = request.Estado, FechaVerificacion = DateTime.UtcNow, Observaciones = request.Observaciones, FechaCreacion = DateTime.UtcNow });
        recepcion.EstadoRecepcion = EstadoRecepcionEnum.EnProceso;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CompletarAsync(int recepcionId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty) return Result.Failure("El usuario es requerido.");
        var recepcion = await db.Recepciones.FindAsync([recepcionId], cancellationToken);
        if (recepcion is null) return Result.Failure("La recepción no existe.");
        if (recepcion.EstadoRecepcion is EstadoRecepcionEnum.Completada or EstadoRecepcionEnum.CompletadaConIncidencia)
            return Result.Failure("La recepción ya fue completada.");

        var totalEquipos = await db.EnvioEquipos.CountAsync(x => x.EnvioId == recepcion.EnvioId, cancellationToken);
        if (totalEquipos == 0) return Result.Failure("El envío no tiene equipos registrados.");
        var equiposVerificados = await db.RecepcionEquipos.CountAsync(x => x.RecepcionId == recepcionId, cancellationToken);
        if (equiposVerificados != totalEquipos) return Result.Failure("Aún existen equipos pendientes de verificación.");

        var conIncidencia = await db.RecepcionEquipos.AnyAsync(
            x => x.RecepcionId == recepcionId && x.EstadoRecepcionEquipo == EstadoRecepcionEquipoEnum.VerificadoConIncidencia,
            cancellationToken);
        recepcion.EstadoRecepcion = conIncidencia ? EstadoRecepcionEnum.CompletadaConIncidencia : EstadoRecepcionEnum.Completada;
        recepcion.UsuarioQueRecibioId = usuarioId;
        recepcion.FechaRecepcion = DateTime.UtcNow;
        recepcion.FechaModificacion = DateTime.UtcNow;
        recepcion.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
