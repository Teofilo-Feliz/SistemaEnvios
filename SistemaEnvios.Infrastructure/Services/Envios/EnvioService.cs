using SistemaEnvios.Application.Common;
using FluentValidation;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Infrastructure.Persistence;
namespace SistemaEnvios.Infrastructure.Services.Envios;

public class EnvioService(
IGenericRepository<Envio> envios,
SistemaEnviosDbContext db,
IUnitOfWork unitOfWork,
IValidator<CrearEnvioRequest> validator) : IEnvioService
{
    public async Task<Result<EnvioResponse>> CrearAsync(CrearEnvioRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<EnvioResponse>.Failure(validation.ToErrorMessage());
        }

        if (request.UbicacionOrigenId == request.UbicacionDestinoId)
            return Result<EnvioResponse>.Failure("La ubicación de origen y destino no pueden ser iguales.");
        if (!await db.Ubicaciones.AnyAsync(x => x.UbicacionId == request.UbicacionOrigenId && x.Activo, cancellationToken) ||
            !await db.Ubicaciones.AnyAsync(x => x.UbicacionId == request.UbicacionDestinoId && x.Activo, cancellationToken))
            return Result<EnvioResponse>.Failure("La ubicación de origen o destino no existe o está inactiva.");
        if (!await db.EstadosEnvio.AnyAsync(x => x.EstadoEnvioId == request.EstadoEnvioId && x.Activo, cancellationToken))
            return Result<EnvioResponse>.Failure("El estado del envío no existe o está inactivo.");
        var envio = new Envio
        {
            NumeroEnvio = $"ENV-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            UbicacionOrigenId = request.UbicacionOrigenId,
            UbicacionDestinoId = request.UbicacionDestinoId,
            EstadoEnvioId = request.EstadoEnvioId,
            UsuarioSolicitanteId = request.UsuarioSolicitanteId,
            Observaciones = request.Observaciones,
            FechaCreacion = DateTime.UtcNow
        };
        await envios.AddAsync(envio, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EnvioResponse>.Success(ToResponse(envio));
    }

    public async Task<Result<EnvioResponse>> ObtenerAsync(int envioId, CancellationToken cancellationToken = default)
    {
        var envio = await envios.GetByIdAsync(envioId, cancellationToken);
        return envio is null ? Result<EnvioResponse>.Failure("El envío no existe.") : Result<EnvioResponse>.Success(ToResponse(envio));
    }

    public async Task<Result<IReadOnlyCollection<EnvioResponse>>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var resultado = await db.Envios.AsNoTracking()
            .OrderByDescending(x => x.FechaCreacion)
            .Select(x => new EnvioResponse(x.EnvioId, x.NumeroEnvio, x.UbicacionOrigenId, x.UbicacionDestinoId, x.EstadoEnvioId, x.UsuarioSolicitanteId, x.Observaciones))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<EnvioResponse>>.Success(resultado);
    }

    private static EnvioResponse ToResponse(Envio e) => new(e.EnvioId, e.NumeroEnvio, e.UbicacionOrigenId, e.UbicacionDestinoId, e.EstadoEnvioId, e.UsuarioSolicitanteId, e.Observaciones);
}
