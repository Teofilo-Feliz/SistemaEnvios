using Microsoft.EntityFrameworkCore;
using FluentValidation;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Transportes;

public sealed class TransporteService : ITransporteService
{
    private readonly SistemaEnviosDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearTransporteRequest> _validator;

    public TransporteService(SistemaEnviosDbContext db, IUnitOfWork unitOfWork, IValidator<CrearTransporteRequest> validator)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<int>> CrearAsync(CrearTransporteRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<int>.Failure(validation.ToErrorMessage());
        }

        if (!await _db.Envios.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken))
        {
            return Result<int>.Failure("El envío no existe.");
        }

        if (await _db.Transportes.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken))
        {
            return Result<int>.Failure("El envío ya tiene transporte registrado.");
        }

        var transporte = new Transporte
        {
            EnvioId = request.EnvioId,
            Tipo = request.Tipo,
            NombreChofer = request.NombreChofer,
            Placa = request.Placa,
            Observaciones = request.Observaciones,
            FechaEntregaTransportacion = DateTime.UtcNow,
            FechaCreacion = DateTime.UtcNow
        };

        _db.Transportes.Add(transporte);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(transporte.TransporteId);
    }

    public async Task<Result<Transporte>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        var transporte = await _db.Transportes.AsNoTracking().FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        return transporte is null ? Result<Transporte>.Failure("El transporte no existe.") : Result<Transporte>.Success(transporte);
    }

    public async Task<Result> ConfirmarAsync(int transporteId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty) return Result.Failure("El usuario es requerido.");
        var transporte = await _db.Transportes.FindAsync([transporteId], cancellationToken);
        if (transporte is null)
        {
            return Result.Failure("El transporte no existe.");
        }

        if (transporte.EntregaConfirmada) return Result.Failure("La entrega del transporte ya fue confirmada.");

        transporte.EntregaConfirmada = true;
        transporte.FechaConfirmacionEntrega = DateTime.UtcNow;
        transporte.UsuarioConfirmacionId = usuarioId;
        transporte.FechaModificacion = DateTime.UtcNow;
        transporte.UsuarioModificacionId = usuarioId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
