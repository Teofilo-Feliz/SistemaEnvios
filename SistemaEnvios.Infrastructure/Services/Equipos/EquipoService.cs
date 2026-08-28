using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Equipos;

public sealed class EquipoService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<Equipo> validator) : IEquipoService
{
    public async Task<Result<Equipo>> ObtenerAsync(int equipoId, CancellationToken cancellationToken = default)
    {
        var equipo = await db.Equipos.AsNoTracking().FirstOrDefaultAsync(x => x.EquipoId == equipoId, cancellationToken);
        return equipo is null ? Result<Equipo>.Failure("El equipo no existe.") : Result<Equipo>.Success(equipo);
    }

    public async Task<Result<IReadOnlyCollection<Equipo>>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var equipos = await db.Equipos.AsNoTracking().OrderBy(x => x.Marca).ThenBy(x => x.Modelo).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<Equipo>>.Success(equipos);
    }

    public async Task<Result<int>> CrearAsync(Equipo equipo, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(equipo, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage());

        if (!await db.TiposEquipo.AnyAsync(x => x.TipoEquipoId == equipo.TipoEquipoId && x.Activo, cancellationToken))
            return Result<int>.Failure("El tipo de equipo no existe o está inactivo.");

        if (!await db.Ubicaciones.AnyAsync(x => x.UbicacionId == equipo.UbicacionActualId && x.Activo, cancellationToken))
            return Result<int>.Failure("La ubicación no existe o está inactiva.");

        var codigoActivo = NormalizarOpcional(equipo.CodigoActivo);
        var numeroSerie = NormalizarOpcional(equipo.NumeroSerie);

        if (codigoActivo is not null &&
            await db.Equipos.AnyAsync(x => x.CodigoActivo == codigoActivo, cancellationToken))
            return Result<int>.Failure("Ya existe un equipo con el código de activo indicado.");

        if (numeroSerie is not null &&
            await db.Equipos.AnyAsync(x => x.NumeroSerie == numeroSerie, cancellationToken))
            return Result<int>.Failure("Ya existe un equipo con el número de serie indicado.");

        equipo.CodigoActivo = codigoActivo;
        equipo.NumeroSerie = numeroSerie;
        equipo.Marca = equipo.Marca.Trim();
        equipo.Modelo = equipo.Modelo.Trim();
        equipo.Observaciones = NormalizarOpcional(equipo.Observaciones);
        equipo.FechaCreacion = DateTime.UtcNow;

        db.Equipos.Add(equipo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(equipo.EquipoId);
    }

    public async Task<Result> ActualizarAsync(Equipo equipo, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(equipo, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage());
        var actual = await db.Equipos.FindAsync([equipo.EquipoId], cancellationToken);
        if (actual is null) return Result.Failure("El equipo no existe.");
        if (!await db.TiposEquipo.AnyAsync(x => x.TipoEquipoId == equipo.TipoEquipoId && x.Activo, cancellationToken))
            return Result.Failure("El tipo de equipo no existe o está inactivo.");
        if (!await db.Ubicaciones.AnyAsync(x => x.UbicacionId == equipo.UbicacionActualId && x.Activo, cancellationToken))
            return Result.Failure("La ubicación no existe o está inactiva.");
        var codigo = NormalizarOpcional(equipo.CodigoActivo);
        var serie = NormalizarOpcional(equipo.NumeroSerie);
        if (codigo is not null && await db.Equipos.AnyAsync(x => x.EquipoId != equipo.EquipoId && x.CodigoActivo == codigo, cancellationToken))
            return Result.Failure("Ya existe un equipo con el código de activo indicado.");
        if (serie is not null && await db.Equipos.AnyAsync(x => x.EquipoId != equipo.EquipoId && x.NumeroSerie == serie, cancellationToken))
            return Result.Failure("Ya existe un equipo con el número de serie indicado.");
        actual.CodigoActivo = codigo;
        actual.NumeroSerie = serie;
        actual.TipoEquipoId = equipo.TipoEquipoId;
        actual.UbicacionActualId = equipo.UbicacionActualId;
        actual.Marca = equipo.Marca.Trim();
        actual.Modelo = equipo.Modelo.Trim();
        actual.Observaciones = NormalizarOpcional(equipo.Observaciones);
        actual.FechaModificacion = DateTime.UtcNow;
        actual.UsuarioModificacionId = equipo.UsuarioModificacionId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
