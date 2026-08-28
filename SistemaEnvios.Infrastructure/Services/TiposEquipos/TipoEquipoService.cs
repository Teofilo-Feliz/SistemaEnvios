using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.TiposEquipos;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.TiposEquipos;

public sealed class TipoEquipoService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<GuardarTipoEquipoRequest> validator,
    IUserContext userContext) : ITipoEquipoService
{
    public async Task<Result<TipoEquipoResponse>> ObtenerAsync(int tipoEquipoId, CancellationToken cancellationToken = default)
    {
        var tipo = await db.TiposEquipo.AsNoTracking().Where(x => x.TipoEquipoId == tipoEquipoId)
            .Select(x => new TipoEquipoResponse(x.TipoEquipoId, x.Nombre, x.Activo)).FirstOrDefaultAsync(cancellationToken);
        return tipo is null ? Result<TipoEquipoResponse>.Failure("El tipo de equipo no existe.", ErrorType.NotFound) : Result<TipoEquipoResponse>.Success(tipo);
    }

    public async Task<Result<IReadOnlyCollection<TipoEquipoResponse>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default)
    {
        var query = db.TiposEquipo.AsNoTracking();
        if (soloActivos) query = query.Where(x => x.Activo);
        var tipos = await query.OrderBy(x => x.Nombre).Select(x => new TipoEquipoResponse(x.TipoEquipoId, x.Nombre, x.Activo)).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<TipoEquipoResponse>>.Success(tipos);
    }

    public async Task<Result<int>> CrearAsync(GuardarTipoEquipoRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        if (request.TipoEquipoId != 0) return Result<int>.Failure("El identificador no debe enviarse al crear un tipo de equipo.", ErrorType.Validation);
        var nombre = request.Nombre.Trim();
        if (await db.TiposEquipo.AnyAsync(x => x.Nombre == nombre, cancellationToken)) return Result<int>.Failure("Ya existe un tipo de equipo con el nombre indicado.", ErrorType.Conflict);

        var tipo = new TipoEquipo { Nombre = nombre, Activo = true, FechaCreacion = DateTime.UtcNow, UsuarioCreacionId = usuarioId };
        db.TiposEquipo.Add(tipo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(tipo.TipoEquipoId);
    }

    public async Task<Result> ActualizarAsync(GuardarTipoEquipoRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        if (request.TipoEquipoId <= 0) return Result.Failure("El identificador del tipo de equipo es requerido.", ErrorType.Validation);
        var tipo = await db.TiposEquipo.FindAsync([request.TipoEquipoId], cancellationToken);
        if (tipo is null) return Result.Failure("El tipo de equipo no existe.", ErrorType.NotFound);
        var nombre = request.Nombre.Trim();
        if (await db.TiposEquipo.AnyAsync(x => x.TipoEquipoId != request.TipoEquipoId && x.Nombre == nombre, cancellationToken)) return Result.Failure("Ya existe un tipo de equipo con el nombre indicado.", ErrorType.Conflict);
        tipo.Nombre = nombre;
        tipo.FechaModificacion = DateTime.UtcNow;
        tipo.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarActivoAsync(int tipoEquipoId, bool activo, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var tipo = await db.TiposEquipo.FindAsync([tipoEquipoId], cancellationToken);
        if (tipo is null) return Result.Failure("El tipo de equipo no existe.", ErrorType.NotFound);
        if (tipo.Activo == activo) return Result.Success();
        tipo.Activo = activo;
        tipo.FechaModificacion = DateTime.UtcNow;
        tipo.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
