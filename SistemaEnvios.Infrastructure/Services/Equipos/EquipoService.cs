using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Equipos;

public sealed class EquipoService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CrearEquipoRequest> crearValidator,
    IValidator<ActualizarEquipoRequest> actualizarValidator,
    IUserContext userContext) : IEquipoService
{
    public async Task<Result<EquipoResponse>> ObtenerAsync(int equipoId, CancellationToken cancellationToken = default)
    {
        var equipo = await db.Equipos.AsNoTracking()
            .Where(x => x.EquipoId == equipoId)
            .Select(x => new EquipoResponse(x.EquipoId, x.CodigoActivo, x.NumeroSerie, x.TipoEquipoId,
                x.UbicacionActualId, x.Marca, x.Modelo, x.Observaciones))
            .FirstOrDefaultAsync(cancellationToken);

        return equipo is null
            ? Result<EquipoResponse>.Failure("El equipo no existe.", ErrorType.NotFound)
            : Result<EquipoResponse>.Success(equipo);
    }

    public async Task<Result<IReadOnlyCollection<EquipoResponse>>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var equipos = await db.Equipos.AsNoTracking()
            .OrderBy(x => x.Marca)
            .ThenBy(x => x.Modelo)
            .Select(x => new EquipoResponse(x.EquipoId, x.CodigoActivo, x.NumeroSerie, x.TipoEquipoId,
                x.UbicacionActualId, x.Marca, x.Modelo, x.Observaciones))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<EquipoResponse>>.Success(equipos);
    }

    public async Task<Result<int>> CrearAsync(CrearEquipoRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await crearValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);

        if (userContext.UserId is not Guid usuarioId)
            return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var referenciasValidas = await ReferenciasValidasAsync(request.TipoEquipoId, request.UbicacionActualId, cancellationToken);
        if (referenciasValidas.IsFailure)
            return Result<int>.Failure(referenciasValidas.Error!, referenciasValidas.ErrorType);

        var codigoActivo = NormalizarOpcional(request.CodigoActivo);
        var numeroSerie = NormalizarOpcional(request.NumeroSerie);
        var unicidad = await ValidarUnicidadAsync(0, codigoActivo, numeroSerie, cancellationToken);
        if (unicidad.IsFailure)
            return Result<int>.Failure(unicidad.Error!, unicidad.ErrorType);

        var equipo = new Equipo
        {
            CodigoActivo = codigoActivo,
            NumeroSerie = numeroSerie,
            TipoEquipoId = request.TipoEquipoId,
            UbicacionActualId = request.UbicacionActualId,
            Marca = request.Marca.Trim(),
            Modelo = request.Modelo.Trim(),
            Observaciones = NormalizarOpcional(request.Observaciones),
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacionId = usuarioId
        };

        db.Equipos.Add(equipo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(equipo.EquipoId);
    }

    public async Task<Result> ActualizarAsync(ActualizarEquipoRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await actualizarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);

        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var equipo = await db.Equipos.FindAsync([request.EquipoId], cancellationToken);
        if (equipo is null)
            return Result.Failure("El equipo no existe.", ErrorType.NotFound);

        var referenciasValidas = await ReferenciasValidasAsync(request.TipoEquipoId, request.UbicacionActualId, cancellationToken);
        if (referenciasValidas.IsFailure)
            return referenciasValidas;

        var codigoActivo = NormalizarOpcional(request.CodigoActivo);
        var numeroSerie = NormalizarOpcional(request.NumeroSerie);
        var unicidad = await ValidarUnicidadAsync(request.EquipoId, codigoActivo, numeroSerie, cancellationToken);
        if (unicidad.IsFailure)
            return unicidad;

        if (equipo.UbicacionActualId != request.UbicacionActualId &&
            await db.ReservasEquipoEnvio.AnyAsync(x => x.EquipoId == request.EquipoId, cancellationToken))
            return Result.Failure(
                "No se puede cambiar la ubicación de un equipo reservado en un envío activo.",
                ErrorType.Conflict);

        equipo.CodigoActivo = codigoActivo;
        equipo.NumeroSerie = numeroSerie;
        equipo.TipoEquipoId = request.TipoEquipoId;
        equipo.UbicacionActualId = request.UbicacionActualId;
        equipo.Marca = request.Marca.Trim();
        equipo.Modelo = request.Modelo.Trim();
        equipo.Observaciones = NormalizarOpcional(request.Observaciones);
        equipo.FechaModificacion = DateTime.UtcNow;
        equipo.UsuarioModificacionId = usuarioId;

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                "El equipo fue modificado o reservado por otra operación. Actualice los datos e intente nuevamente.",
                ErrorType.Conflict);
        }
        return Result.Success();
    }

    private async Task<Result> ReferenciasValidasAsync(int tipoEquipoId, int ubicacionId, CancellationToken cancellationToken)
    {
        if (!await db.TiposEquipo.AnyAsync(x => x.TipoEquipoId == tipoEquipoId && x.Activo, cancellationToken))
            return Result.Failure("El tipo de equipo no existe o está inactivo.", ErrorType.Validation);
        if (!await db.Ubicaciones.AnyAsync(x => x.UbicacionId == ubicacionId && x.Activo, cancellationToken))
            return Result.Failure("La ubicación no existe o está inactiva.", ErrorType.Validation);
        return Result.Success();
    }

    private async Task<Result> ValidarUnicidadAsync(int equipoId, string? codigoActivo, string? numeroSerie, CancellationToken cancellationToken)
    {
        if (codigoActivo is not null && await db.Equipos.AnyAsync(x => x.EquipoId != equipoId && x.CodigoActivo == codigoActivo, cancellationToken))
            return Result.Failure("Ya existe un equipo con el código de activo indicado.", ErrorType.Conflict);
        if (numeroSerie is not null && await db.Equipos.AnyAsync(x => x.EquipoId != equipoId && x.NumeroSerie == numeroSerie, cancellationToken))
            return Result.Failure("Ya existe un equipo con el número de serie indicado.", ErrorType.Conflict);
        return Result.Success();
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
