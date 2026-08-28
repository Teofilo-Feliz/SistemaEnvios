using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Transportes;

public sealed class TransporteService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CrearTransporteRequest> validator,
    IValidator<ActualizarTransporteRequest> actualizarValidator,
    IUserContext userContext) : ITransporteService
{
    public async Task<Result<int>> CrearAsync(CrearTransporteRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var envio = await db.Envios.Include(x => x.EstadoEnvio).FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);
        if (envio is null) return Result<int>.Failure("El envío no existe.", ErrorType.NotFound);
        if (!EsEstadoEditable(envio.EstadoEnvio.Codigo)) return Result<int>.Failure("El envío ya fue despachado y no admite cambios en el transporte.", ErrorType.Conflict);
        if (await db.Transportes.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken)) return Result<int>.Failure("El envío ya tiene transporte registrado.", ErrorType.Conflict);

        var fecha = DateTime.UtcNow;
        var transporte = new Transporte
        {
            EnvioId = request.EnvioId,
            Tipo = request.Tipo.Trim(),
            NombreChofer = NormalizarOpcional(request.NombreChofer),
            Placa = NormalizarOpcional(request.Placa),
            Observaciones = NormalizarOpcional(request.Observaciones),
            FechaCreacion = fecha,
            UsuarioCreacionId = usuarioId
        };
        db.Transportes.Add(transporte);
        MarcarEnvioModificado(envio, usuarioId);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<int>.Failure(
                "El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.",
                ErrorType.Conflict);
        }
        return Result<int>.Success(transporte.TransporteId);
    }

    public async Task<Result<TransporteResponse>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        var transporte = await db.Transportes.AsNoTracking().Where(x => x.EnvioId == envioId)
            .Select(x => new TransporteResponse(x.TransporteId, x.EnvioId, x.Tipo, x.NombreChofer, x.Placa,
                x.FechaEntregaTransportacion, x.Observaciones, x.EntregaConfirmada,
                x.FechaConfirmacionEntrega, x.UsuarioConfirmacionId))
            .FirstOrDefaultAsync(cancellationToken);
        return transporte is null ? Result<TransporteResponse>.Failure("El transporte no existe.", ErrorType.NotFound) : Result<TransporteResponse>.Success(transporte);
    }

    public async Task<Result> ConfirmarAsync(int transporteId, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var transporte = await db.Transportes.Include(x => x.Envio).ThenInclude(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.TransporteId == transporteId, cancellationToken);
        if (transporte is null) return Result.Failure("El transporte no existe.", ErrorType.NotFound);
        if (transporte.Envio.EstadoEnvio.EsFinal) return Result.Failure("El envío está finalizado y no admite cambios.", ErrorType.Conflict);
        if (transporte.EntregaConfirmada) return Result.Failure("La entrega del transporte ya fue confirmada.", ErrorType.Conflict);
        if (transporte.FechaEntregaTransportacion is null)
            return Result.Failure("El envío todavía no ha sido entregado a transportación.", ErrorType.Conflict);
        if (transporte.Envio.EstadoEnvio.Codigo != EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion)
            return Result.Failure("El envío no está pendiente de confirmación por transportación.", ErrorType.Conflict);

        var destino = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.Codigo == EstadoEnvioCodigos.ConfirmadoPorTransportacion && x.Activo, cancellationToken);
        if (destino is null) return Result.Failure("El estado de confirmación no se encuentra configurado.", ErrorType.Conflict);
        if (!await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == transporte.Envio.EstadoEnvioId && x.EstadoDestinoId == destino.EstadoEnvioId && x.Activo, cancellationToken))
            return Result.Failure("La transición de confirmación no está permitida.", ErrorType.Conflict);

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var fecha = DateTime.UtcNow;
            transporte.EntregaConfirmada = true;
            transporte.FechaConfirmacionEntrega = fecha;
            transporte.UsuarioConfirmacionId = usuarioId;
            transporte.FechaModificacion = fecha;
            transporte.UsuarioModificacionId = usuarioId;
            transporte.Envio.EstadoEnvioId = destino.EstadoEnvioId;
            transporte.Envio.FechaModificacion = fecha;
            transporte.Envio.UsuarioModificacionId = usuarioId;
            db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
            {
                EnvioId = transporte.EnvioId,
                EstadoEnvioId = destino.EstadoEnvioId,
                UbicacionId = transporte.Envio.UbicacionOrigenId,
                UsuarioId = usuarioId,
                Fecha = fecha,
                Observaciones = "Transportación confirmó la custodia del envío.",
                FechaCreacion = fecha,
                UsuarioCreacionId = usuarioId
            });
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure(
                "El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.",
                ErrorType.Conflict);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result> ActualizarAsync(
        ActualizarTransporteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await actualizarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var transporte = await db.Transportes.Include(x => x.Envio).ThenInclude(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.TransporteId == request.TransporteId, cancellationToken);
        if (transporte is null)
            return Result.Failure("El transporte no existe.", ErrorType.NotFound);
        if (!EsEstadoEditable(transporte.Envio.EstadoEnvio.Codigo))
            return Result.Failure("El envío ya fue despachado y no admite cambios en el transporte.", ErrorType.Conflict);

        transporte.Tipo = request.Tipo.Trim();
        transporte.NombreChofer = NormalizarOpcional(request.NombreChofer);
        transporte.Placa = NormalizarOpcional(request.Placa);
        transporte.Observaciones = NormalizarOpcional(request.Observaciones);
        transporte.FechaModificacion = DateTime.UtcNow;
        transporte.UsuarioModificacionId = usuarioId;
        MarcarEnvioModificado(transporte.Envio, usuarioId);
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

    private static bool EsEstadoEditable(string codigo) => codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EnPreparacionTecnologia;
    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static void MarcarEnvioModificado(Envio envio, Guid usuarioId)
    {
        envio.FechaModificacion = DateTime.UtcNow;
        envio.UsuarioModificacionId = usuarioId;
    }
}
