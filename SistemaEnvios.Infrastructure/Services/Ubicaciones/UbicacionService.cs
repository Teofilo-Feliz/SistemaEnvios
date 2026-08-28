using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Ubicaciones;

public sealed class UbicacionService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<Ubicacion> validator) : IUbicacionService
{
    public async Task<Result<Ubicacion>> ObtenerAsync(int ubicacionId, CancellationToken cancellationToken = default)
    {
        var ubicacion = await db.Ubicaciones.AsNoTracking().FirstOrDefaultAsync(x => x.UbicacionId == ubicacionId, cancellationToken);
        return ubicacion is null ? Result<Ubicacion>.Failure("La ubicación no existe.") : Result<Ubicacion>.Success(ubicacion);
    }

    public async Task<Result<IReadOnlyCollection<Ubicacion>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default)
    {
        var query = db.Ubicaciones.AsNoTracking();
        if (soloActivas) query = query.Where(x => x.Activo);
        var ubicaciones = await query.OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<Ubicacion>>.Success(ubicaciones);
    }

    public async Task<Result<int>> CrearAsync(Ubicacion ubicacion, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(ubicacion, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage());

        var codigoCentro = ubicacion.CodigoCentro.Trim();
        if (await db.Ubicaciones.AnyAsync(x => x.CodigoCentro == codigoCentro, cancellationToken))
            return Result<int>.Failure("Ya existe una ubicación con el código de centro indicado.");

        ubicacion.Nombre = ubicacion.Nombre.Trim();
        ubicacion.CodigoCentro = codigoCentro;
        ubicacion.FechaCreacion = DateTime.UtcNow;
        db.Ubicaciones.Add(ubicacion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(ubicacion.UbicacionId);
    }

    public async Task<Result> ActualizarAsync(Ubicacion ubicacion, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(ubicacion, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage());
        var actual = await db.Ubicaciones.FindAsync([ubicacion.UbicacionId], cancellationToken);
        if (actual is null) return Result.Failure("La ubicación no existe.");
        var codigo = ubicacion.CodigoCentro.Trim();
        if (await db.Ubicaciones.AnyAsync(x => x.UbicacionId != ubicacion.UbicacionId && x.CodigoCentro == codigo, cancellationToken))
            return Result.Failure("Ya existe una ubicación con el código de centro indicado.");
        actual.Nombre = ubicacion.Nombre.Trim();
        actual.CodigoCentro = codigo;
        actual.FechaModificacion = DateTime.UtcNow;
        actual.UsuarioModificacionId = ubicacion.UsuarioModificacionId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarActivoAsync(int ubicacionId, bool activo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty) return Result.Failure("El usuario es requerido.");
        var ubicacion = await db.Ubicaciones.FindAsync([ubicacionId], cancellationToken);
        if (ubicacion is null) return Result.Failure("La ubicación no existe.");
        if (ubicacion.Activo == activo) return Result.Success();
        ubicacion.Activo = activo;
        ubicacion.FechaModificacion = DateTime.UtcNow;
        ubicacion.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
