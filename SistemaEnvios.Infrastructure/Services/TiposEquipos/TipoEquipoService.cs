using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.TiposEquipos;

public sealed class TipoEquipoService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<TipoEquipo> validator) : ITipoEquipoService
{
    public async Task<Result<TipoEquipo>> ObtenerAsync(int tipoEquipoId, CancellationToken cancellationToken = default)
    {
        var tipo = await db.TiposEquipo.AsNoTracking().FirstOrDefaultAsync(x => x.TipoEquipoId == tipoEquipoId, cancellationToken);
        return tipo is null ? Result<TipoEquipo>.Failure("El tipo de equipo no existe.") : Result<TipoEquipo>.Success(tipo);
    }

    public async Task<Result<IReadOnlyCollection<TipoEquipo>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default)
    {
        var query = db.TiposEquipo.AsNoTracking();
        if (soloActivos) query = query.Where(x => x.Activo);
        var tipos = await query.OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<TipoEquipo>>.Success(tipos);
    }

    public async Task<Result<int>> CrearAsync(TipoEquipo tipoEquipo, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(tipoEquipo, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage());

        var nombre = tipoEquipo.Nombre.Trim();
        if (await db.TiposEquipo.AnyAsync(x => x.Nombre == nombre, cancellationToken))
            return Result<int>.Failure("Ya existe un tipo de equipo con el nombre indicado.");

        tipoEquipo.Nombre = nombre;
        tipoEquipo.FechaCreacion = DateTime.UtcNow;
        db.TiposEquipo.Add(tipoEquipo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(tipoEquipo.TipoEquipoId);
    }

    public async Task<Result> ActualizarAsync(TipoEquipo tipoEquipo, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(tipoEquipo, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage());
        var actual = await db.TiposEquipo.FindAsync([tipoEquipo.TipoEquipoId], cancellationToken);
        if (actual is null) return Result.Failure("El tipo de equipo no existe.");
        var nombre = tipoEquipo.Nombre.Trim();
        if (await db.TiposEquipo.AnyAsync(x => x.TipoEquipoId != tipoEquipo.TipoEquipoId && x.Nombre == nombre, cancellationToken))
            return Result.Failure("Ya existe un tipo de equipo con el nombre indicado.");
        actual.Nombre = nombre;
        actual.FechaModificacion = DateTime.UtcNow;
        actual.UsuarioModificacionId = tipoEquipo.UsuarioModificacionId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarActivoAsync(int tipoEquipoId, bool activo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty) return Result.Failure("El usuario es requerido.");
        var tipo = await db.TiposEquipo.FindAsync([tipoEquipoId], cancellationToken);
        if (tipo is null) return Result.Failure("El tipo de equipo no existe.");
        if (tipo.Activo == activo) return Result.Success();
        tipo.Activo = activo;
        tipo.FechaModificacion = DateTime.UtcNow;
        tipo.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
