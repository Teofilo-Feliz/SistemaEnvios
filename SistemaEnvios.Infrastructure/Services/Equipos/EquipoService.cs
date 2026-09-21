using FluentValidation;
using SistemaEnvios.Application.DTOs.Common;
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
    IUserContext userContext,
    IAlcanceEnvios alcance) : IEquipoService
{
    public async Task<Result<EquipoResponse>> ObtenerAsync(int equipoId, CancellationToken cancellationToken = default)
    {
        var alcanzable = await (await alcance.FiltrarEquiposAsync(db.Equipos.AsNoTracking(), cancellationToken))
            .AnyAsync(x => x.EquipoId == equipoId, cancellationToken);
        if (!alcanzable)
            return await db.Equipos.AnyAsync(x => x.EquipoId == equipoId, cancellationToken)
                ? Result<EquipoResponse>.Failure("El equipo no pertenece a su filial.", ErrorType.Forbidden)
                : Result<EquipoResponse>.Failure("El equipo no existe.", ErrorType.NotFound);

        var equipo = await db.Equipos.AsNoTracking()
            .Where(x => x.EquipoId == equipoId)
            .Select(x => new EquipoResponse(x.EquipoId, x.CodigoActivo, x.NumeroSerie, x.TipoEquipoId,
                x.UbicacionActualId, x.Marca, x.Modelo, x.Observaciones))
            .FirstOrDefaultAsync(cancellationToken);

        return equipo is null
            ? Result<EquipoResponse>.Failure("El equipo no existe.", ErrorType.NotFound)
            : Result<EquipoResponse>.Success(equipo);
    }

    public async Task<Result<PaginaResponse<ViajeEquipoResponse>>> ListarViajesAsync(
        int equipoId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default)
    {
        // Mismo alcance que ver la ficha: si puede abrir el equipo, puede ver por dónde anduvo.
        // Se distingue "no existe" de "no le corresponde" para no mandar a nadie a buscar un
        // permiso que no le falta.
        var alcanzable = await (await alcance.FiltrarEquiposAsync(db.Equipos.AsNoTracking(), cancellationToken))
            .AnyAsync(x => x.EquipoId == equipoId, cancellationToken);
        if (!alcanzable)
            return await db.Equipos.AnyAsync(x => x.EquipoId == equipoId, cancellationToken)
                ? Result<PaginaResponse<ViajeEquipoResponse>>.Failure("El equipo no pertenece a su filial.", ErrorType.Forbidden)
                : Result<PaginaResponse<ViajeEquipoResponse>>.Failure("El equipo no existe.", ErrorType.NotFound);

        var pagina = await db.EnvioEquipos.AsNoTracking()
            .Where(x => x.EquipoId == equipoId)
            // Lo último primero: quien abre la ficha quiere saber dónde está ahora.
            .OrderByDescending(x => x.Envio.FechaCreacion).ThenByDescending(x => x.EnvioEquipoId)
            .PaginarAsync(request, x => new ViajeEquipoResponse(
                x.EnvioId,
                x.Envio.NumeroEnvio,
                x.Envio.FechaCreacion,
                x.Envio.UbicacionOrigen.Nombre,
                x.Envio.UbicacionDestino.Nombre,
                x.Envio.EstadoEnvio.Codigo,
                x.Envio.EstadoEnvio.Nombre,
                x.NumeroTicket,
                x.EnvioEquipoOrigenId == null,
                x.Observaciones), cancellationToken);

        return Result<PaginaResponse<ViajeEquipoResponse>>.Success(pagina);
    }

    public async Task<Result<PaginaResponse<EquipoResponse>>> ListarAsync(ConsultarEquiposRequest request, CancellationToken cancellationToken = default)
    {
        var query = await alcance.FiltrarEquiposAsync(db.Equipos.AsNoTracking(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var termino = request.Search.Trim();
            query = query.Where(x =>
                (x.NumeroSerie != null && x.NumeroSerie.Contains(termino)) ||
                (x.CodigoActivo != null && x.CodigoActivo.Contains(termino)) ||
                x.Marca.Contains(termino) || x.Modelo.Contains(termino));
        }
        if (request.TipoEquipoId.HasValue) query = query.Where(x => x.TipoEquipoId == request.TipoEquipoId);
        if (request.UbicacionActualId.HasValue) query = query.Where(x => x.UbicacionActualId == request.UbicacionActualId);

        var pagina = await query.OrderBy(x => x.Marca).ThenBy(x => x.Modelo).ThenBy(x => x.EquipoId)
            .PaginarAsync(request, x => new EquipoResponse(x.EquipoId, x.CodigoActivo, x.NumeroSerie, x.TipoEquipoId,
                x.UbicacionActualId, x.Marca, x.Modelo, x.Observaciones), cancellationToken);

        return Result<PaginaResponse<EquipoResponse>>.Success(pagina);
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

        var destinoPermitido = await alcance.VerificarUbicacionAsync(request.UbicacionActualId, cancellationToken);
        if (destinoPermitido.IsFailure)
            return Result<int>.Failure(destinoPermitido.Error!, destinoPermitido.ErrorType);

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

    public async Task<Result<bool>> CodigoActivoDisponibleAsync(
        string codigoActivo, int? excluirEquipoId = null, CancellationToken cancellationToken = default)
    {
        var codigo = NormalizarOpcional(codigoActivo);
        // Sin código no hay nada que chocar: la columna admite nulos y el índice único los ignora.
        if (codigo is null) return Result<bool>.Success(true);

        var ocupado = await db.Equipos.AsNoTracking().AnyAsync(
            x => x.CodigoActivo == codigo && (!excluirEquipoId.HasValue || x.EquipoId != excluirEquipoId.Value),
            cancellationToken);

        return Result<bool>.Success(!ocupado);
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

        var origenPermitido = await alcance.VerificarUbicacionAsync(equipo.UbicacionActualId, cancellationToken);
        if (origenPermitido.IsFailure)
            return Result.Failure("El equipo no pertenece a su filial.", origenPermitido.ErrorType);

        var destinoPermitido = await alcance.VerificarUbicacionAsync(request.UbicacionActualId, cancellationToken);
        if (destinoPermitido.IsFailure)
            return destinoPermitido;

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
