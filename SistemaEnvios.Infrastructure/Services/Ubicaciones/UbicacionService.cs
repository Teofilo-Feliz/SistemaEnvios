using FluentValidation;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Ubicaciones;

public sealed class UbicacionService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<GuardarUbicacionRequest> validator,
    IUserContext userContext) : IUbicacionService
{
    public async Task<Result<UbicacionResponse>> ObtenerAsync(int ubicacionId, CancellationToken cancellationToken = default)
    {
        var ubicacion = await db.Ubicaciones.AsNoTracking()
            .Where(x => x.UbicacionId == ubicacionId)
            .Select(x => new UbicacionResponse(x.UbicacionId, x.Nombre, x.CodigoCentro, x.Tipo, x.Activo))
            .FirstOrDefaultAsync(cancellationToken);
        return ubicacion is null
            ? Result<UbicacionResponse>.Failure("La ubicación no existe.", ErrorType.NotFound)
            : Result<UbicacionResponse>.Success(ubicacion);
    }

    public async Task<Result<PaginaResponse<UbicacionResponse>>> ListarAsync(ConsultarCatalogoRequest request, CancellationToken cancellationToken = default)
    {
        var query = db.Ubicaciones.AsNoTracking();
        if (request.SoloActivos) query = query.Where(x => x.Activo);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var termino = request.Search.Trim();
            query = query.Where(x => x.Nombre.Contains(termino) || x.CodigoCentro.Contains(termino));
        }
        var pagina = await query.OrderBy(x => x.Nombre).ThenBy(x => x.UbicacionId)
            .PaginarAsync(request, x => new UbicacionResponse(x.UbicacionId, x.Nombre, x.CodigoCentro, x.Tipo, x.Activo), cancellationToken);
        return Result<PaginaResponse<UbicacionResponse>>.Success(pagina);
    }

    public async Task<Result<int>> CrearAsync(GuardarUbicacionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        if (request.UbicacionId != 0) return Result<int>.Failure("El identificador no debe enviarse al crear una ubicación.", ErrorType.Validation);

        var codigo = request.CodigoCentro.Trim();
        if (await db.Ubicaciones.AnyAsync(x => x.CodigoCentro == codigo, cancellationToken))
            return Result<int>.Failure("Ya existe una ubicación con el código de centro indicado.", ErrorType.Conflict);

        var ubicacion = new Ubicacion
        {
            Nombre = request.Nombre.Trim(),
            CodigoCentro = codigo,
            Tipo = request.Tipo,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId
        };
        db.Ubicaciones.Add(ubicacion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(ubicacion.UbicacionId);
    }

    public async Task<Result> ActualizarAsync(GuardarUbicacionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        if (request.UbicacionId <= 0) return Result.Failure("El identificador de la ubicación es requerido.", ErrorType.Validation);

        var ubicacion = await db.Ubicaciones.FindAsync([request.UbicacionId], cancellationToken);
        if (ubicacion is null) return Result.Failure("La ubicación no existe.", ErrorType.NotFound);
        var codigo = request.CodigoCentro.Trim();
        if (await db.Ubicaciones.AnyAsync(x => x.UbicacionId != request.UbicacionId && x.CodigoCentro == codigo, cancellationToken))
            return Result.Failure("Ya existe una ubicación con el código de centro indicado.", ErrorType.Conflict);

        ubicacion.Nombre = request.Nombre.Trim();
        ubicacion.CodigoCentro = codigo;
        ubicacion.Tipo = request.Tipo;
        ubicacion.FechaModificacion = DateTime.UtcNow;
        ubicacion.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarActivoAsync(int ubicacionId, bool activo, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var ubicacion = await db.Ubicaciones.FindAsync([ubicacionId], cancellationToken);
        if (ubicacion is null) return Result.Failure("La ubicación no existe.", ErrorType.NotFound);
        if (ubicacion.Activo == activo) return Result.Success();
        ubicacion.Activo = activo;
        ubicacion.FechaModificacion = DateTime.UtcNow;
        ubicacion.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
