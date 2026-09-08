using FluentValidation;
using SistemaEnvios.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
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
            .Select(x => new UbicacionResponse(x.UbicacionId, x.Nombre, x.CodigoCentro, x.Tipo, x.Activo, x.FilialExternaId))
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
            .PaginarAsync(request, x => new UbicacionResponse(x.UbicacionId, x.Nombre, x.CodigoCentro, x.Tipo, x.Activo, x.FilialExternaId), cancellationToken);
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

        // Dos ubicaciones con el mismo id de AuthManager dejarían el alcance a suertes: el filtro
        // por filial buscaría una y encontraría dos, y qué envíos ve el usuario dependería del
        // orden de la consulta.
        if (request.FilialExternaId is int externaNueva &&
            await db.Ubicaciones.AnyAsync(x => x.FilialExternaId == externaNueva, cancellationToken))
            return Result<int>.Failure(
                $"La filial {externaNueva} de AuthManager ya está asociada a otra ubicación.", ErrorType.Conflict);

        var ubicacion = new Ubicacion
        {
            Nombre = request.Nombre.Trim(),
            CodigoCentro = codigo,
            Tipo = request.Tipo,
            FilialExternaId = request.FilialExternaId,
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

        // Tipo e id de AuthManager quedan fijos al crear el centro. Van juntos porque cada tipo
        // exige un estado distinto del id —una filial lo necesita, Tecnología no puede tenerlo—,
        // así que cambiar uno obliga a cambiar el otro.
        //
        // No se permite por dos razones. La de seguridad: quien tenga 'catalogos.administrar'
        // podría apuntar su filial al id de otra y quedarse con el alcance de esa otra; por eso
        // 20260902_PermisosBaseDatos.sql deniega el UPDATE de esa columna al usuario del API, y
        // el intento acabaría en un error 230 de SQL Server disfrazado de 500. La de datos: la
        // dirección de los envíos ya creados se decidió con el tipo que el centro tenía y quedó
        // guardada; cambiarlo dejaría el histórico diciendo una cosa y los datos otra.
        //
        // Si un centro se creó mal, se crea el correcto y se deshabilita el equivocado.
        if (request.Tipo != ubicacion.Tipo)
            return Result.Failure(
                "El tipo de un centro no se cambia después de crearlo: la dirección de sus envíos " +
                "se decidió con él. Cree el centro correcto y deshabilite este.",
                ErrorType.Conflict);

        if (request.FilialExternaId != ubicacion.FilialExternaId)
            return Result.Failure(
                "El id de AuthManager de un centro existente no se cambia desde aquí: es lo que " +
                "decide qué envíos ve cada filial. Lo ajusta Tecnología directamente en la base.",
                ErrorType.Forbidden);

        ubicacion.Nombre = request.Nombre.Trim();
        ubicacion.CodigoCentro = codigo;
        ubicacion.Tipo = request.Tipo;
        // FilialExternaId no se asigna: ya se comprobó que no cambia, y tocarla haría que EF
        // emitiera el UPDATE que la base tiene denegado.
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

        // Todo envío va de una filial a Tecnología o al revés (DeterminarDireccion no admite otra
        // combinación). Sin una Tecnología activa no se puede crear ni un envío en todo el
        // sistema, y el error saldría en la pantalla de crear envío, lejos de aquí, sin que nadie
        // relacione una cosa con la otra.
        if (!activo && ubicacion.Tipo == TipoUbicacionEnum.Tecnologia &&
            !await db.Ubicaciones.AnyAsync(
                x => x.UbicacionId != ubicacionId && x.Tipo == TipoUbicacionEnum.Tecnologia && x.Activo,
                cancellationToken))
        {
            return Result.Failure(
                "Es el único centro de Tecnología activo. Sin él no se puede crear ningún envío, " +
                "porque todo envío va entre una filial y Tecnología.",
                ErrorType.Conflict);
        }

        ubicacion.Activo = activo;
        ubicacion.FechaModificacion = DateTime.UtcNow;
        ubicacion.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
