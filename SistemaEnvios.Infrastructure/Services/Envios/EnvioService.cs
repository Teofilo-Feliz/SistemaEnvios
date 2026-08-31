using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Envios;

public sealed class EnvioService(
    IGenericRepository<Envio> envios,
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<CrearEnvioRequest> validator,
    IValidator<ActualizarEnvioRequest> actualizarValidator,
    IUserContext userContext) : IEnvioService
{
    public async Task<Result<EnvioResponse>> CrearConEquiposAsync(CrearEnvioConEquiposRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Equipos is null || request.Equipos.Count == 0)
            return Result<EnvioResponse>.Failure("El envío debe incluir al menos un equipo.", ErrorType.Validation);
        var tickets = request.Equipos.Select(x => x.NumeroTicket?.Trim()).ToList();
        if (tickets.Any(x => string.IsNullOrWhiteSpace(x) || x.Length is < 3 or > 50 || !x.All(char.IsDigit)) || tickets.Distinct(StringComparer.Ordinal).Count() != tickets.Count)
            return Result<EnvioResponse>.Failure("Los tickets deben ser numéricos, tener entre 3 y 50 dígitos y no repetirse.", ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result<EnvioResponse>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var envioResult = await CrearAsync(new CrearEnvioRequest { UbicacionOrigenId = request.UbicacionOrigenId, UbicacionDestinoId = request.UbicacionDestinoId, Observaciones = request.Observaciones }, cancellationToken);
        if (envioResult.IsFailure) return envioResult;
        foreach (var item in request.Equipos)
        {
            var equipo = await db.Equipos.FindAsync([item.EquipoId], cancellationToken);
            if (equipo is null || equipo.UbicacionActualId != request.UbicacionOrigenId)
                return Result<EnvioResponse>.Failure("Uno de los equipos no existe o no está en el origen.", ErrorType.Conflict);
            if (await db.EnvioEquipos.AnyAsync(x => x.NumeroTicket == item.NumeroTicket.Trim(), cancellationToken) || await db.ReservasEquipoEnvio.AnyAsync(x => x.EquipoId == item.EquipoId, cancellationToken))
                return Result<EnvioResponse>.Failure($"El ticket {item.NumeroTicket} o el equipo ya pertenece a otro envío.", ErrorType.Conflict);
            db.EnvioEquipos.Add(new Domain.Entities.EnvioEquipo { EnvioId = envioResult.Value!.EnvioId, EquipoId = item.EquipoId, NumeroTicket = item.NumeroTicket.Trim(), Observaciones = item.Observaciones?.Trim() ?? "Equipo asociado al envío.", UsuarioSolicitanteId = usuarioId, FechaCreacion = DateTime.UtcNow, UsuarioCreacionId = usuarioId });
            db.ReservasEquipoEnvio.Add(new Domain.Entities.ReservaEquipoEnvio { EquipoId = item.EquipoId, EnvioId = envioResult.Value.EnvioId, FechaReserva = DateTime.UtcNow, UsuarioId = usuarioId });
        }
        try { await unitOfWork.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return envioResult; }
        catch (DbUpdateException) { await transaction.RollbackAsync(cancellationToken); return Result<EnvioResponse>.Failure("No fue posible asociar los equipos; el envío no se creó.", ErrorType.Conflict); }
    }
    public async Task<Result<EnvioResponse>> CrearAsync(
        CrearEnvioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result<EnvioResponse>.Failure(validation.ToErrorMessage(), ErrorType.Validation);

        if (userContext.UserId is not Guid usuarioId)
            return Result<EnvioResponse>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var ubicaciones = await db.Ubicaciones
            .Where(x => (x.UbicacionId == request.UbicacionOrigenId ||
                         x.UbicacionId == request.UbicacionDestinoId) && x.Activo)
            .ToDictionaryAsync(x => x.UbicacionId, cancellationToken);

        if (ubicaciones.Count != 2)
            return Result<EnvioResponse>.Failure("La ubicación de origen o destino no existe o está inactiva.", ErrorType.Validation);

        var origen = ubicaciones[request.UbicacionOrigenId];
        var destino = ubicaciones[request.UbicacionDestinoId];
        var direccionResult = DeterminarDireccion(origen, destino);

        if (direccionResult.IsFailure)
            return Result<EnvioResponse>.Failure(direccionResult.Error!, direccionResult.ErrorType);

        var direccion = direccionResult.Value;
        var codigoInicial = direccion == DireccionEnvioEnum.HaciaTecnologia
            ? EstadoEnvioCodigos.EnFilial
            : EstadoEnvioCodigos.EnPreparacionTecnologia;
        var estadoInicial = await db.EstadosEnvio.FirstOrDefaultAsync(
            x => x.Codigo == codigoInicial && x.Activo,
            cancellationToken);

        if (estadoInicial is null)
            return Result<EnvioResponse>.Failure("El estado inicial del flujo no se encuentra configurado.", ErrorType.Conflict);

        var fechaActual = DateTime.UtcNow;
        var envio = new Envio
        {
            NumeroEnvio = $"ENV-{fechaActual:yyyy}-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            UbicacionOrigenId = request.UbicacionOrigenId,
            UbicacionDestinoId = request.UbicacionDestinoId,
            EstadoEnvioId = estadoInicial.EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = usuarioId,
            Observaciones = NormalizarOpcional(request.Observaciones),
            FechaCreacion = fechaActual,
            UsuarioCreacionId = usuarioId
        };

        await envios.AddAsync(envio, cancellationToken);
        db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
        {
            Envio = envio,
            EstadoEnvioId = estadoInicial.EstadoEnvioId,
            UbicacionId = request.UbicacionOrigenId,
            UsuarioId = usuarioId,
            Fecha = fechaActual,
            Observaciones = "Envío creado.",
            FechaCreacion = fechaActual,
            UsuarioCreacionId = usuarioId
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EnvioResponse>.Success(ToResponse(envio));
    }

    public async Task<Result<EnvioResponse>> ObtenerAsync(
        int envioId,
        CancellationToken cancellationToken = default)
    {
        var envio = await envios.GetByIdAsync(envioId, cancellationToken);
        return envio is null
            ? Result<EnvioResponse>.Failure("El envío no existe.", ErrorType.NotFound)
            : Result<EnvioResponse>.Success(ToResponse(envio));
    }

    public async Task<Result<IReadOnlyCollection<EnvioResponse>>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var resultado = await db.Envios
            .AsNoTracking()
            .OrderByDescending(x => x.FechaCreacion)
            .Select(x => new EnvioResponse(
                x.EnvioId,
                x.NumeroEnvio,
                x.UbicacionOrigenId,
                x.UbicacionDestinoId,
                x.EstadoEnvioId,
                x.Direccion,
                x.UsuarioSolicitanteId,
                x.Observaciones,
                x.Transporte == null ? null : x.Transporte.TipoTransporteId,
                x.Transporte == null ? null : x.Transporte.TipoTransporte.Estrategia,
                x.Transporte == null ? null : x.Transporte.TipoTransporte.Nombre))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<EnvioResponse>>.Success(resultado);
    }

    public async Task<Result<PaginaEnviosResponse>> ConsultarAsync(ConsultarEnviosRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page); var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = db.Envios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search)) { var term = request.Search.Trim(); query = query.Where(x => x.NumeroEnvio.Contains(term) || (x.Observaciones != null && x.Observaciones.Contains(term))); }
        if (request.EstadoEnvioId.HasValue) query = query.Where(x => x.EstadoEnvioId == request.EstadoEnvioId);
        if (request.TipoTransporteId.HasValue) query = query.Where(x => x.Transporte != null && x.Transporte.TipoTransporteId == request.TipoTransporteId);
        if (request.UbicacionOrigenId.HasValue) query = query.Where(x => x.UbicacionOrigenId == request.UbicacionOrigenId);
        if (request.UbicacionDestinoId.HasValue) query = query.Where(x => x.UbicacionDestinoId == request.UbicacionDestinoId);
        if (request.Direccion is 1 or 2) query = query.Where(x => (int)x.Direccion == request.Direccion);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.FechaCreacion).ThenByDescending(x => x.EnvioId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new EnvioResponse(x.EnvioId,x.NumeroEnvio,x.UbicacionOrigenId,x.UbicacionDestinoId,x.EstadoEnvioId,x.Direccion,x.UsuarioSolicitanteId,x.Observaciones,x.Transporte == null ? null : x.Transporte.TipoTransporteId,x.Transporte == null ? null : x.Transporte.TipoTransporte.Estrategia,x.Transporte == null ? null : x.Transporte.TipoTransporte.Nombre)).ToListAsync(cancellationToken);
        return Result<PaginaEnviosResponse>.Success(new(items,page,pageSize,total,(int)Math.Ceiling(total/(double)pageSize)));
    }

    public async Task<Result> ActualizarAsync(
        ActualizarEnvioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await actualizarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var envio = await db.Envios.Include(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);
        if (envio is null)
            return Result.Failure("El envío no existe.", ErrorType.NotFound);
        if (!EsEstadoEditable(envio.EstadoEnvio.Codigo))
            return Result.Failure("El envío ya fue despachado y no admite modificaciones.", ErrorType.Conflict);

        var ubicaciones = await db.Ubicaciones
            .Where(x => (x.UbicacionId == request.UbicacionOrigenId ||
                         x.UbicacionId == request.UbicacionDestinoId) && x.Activo)
            .ToDictionaryAsync(x => x.UbicacionId, cancellationToken);
        if (ubicaciones.Count != 2)
            return Result.Failure("La ubicación de origen o destino no existe o está inactiva.", ErrorType.Validation);

        var direccionResult = DeterminarDireccion(
            ubicaciones[request.UbicacionOrigenId],
            ubicaciones[request.UbicacionDestinoId]);
        if (direccionResult.IsFailure)
            return Result.Failure(direccionResult.Error!, direccionResult.ErrorType);

        var codigoInicial = direccionResult.Value == DireccionEnvioEnum.HaciaTecnologia
            ? EstadoEnvioCodigos.EnFilial
            : EstadoEnvioCodigos.EnPreparacionTecnologia;
        var estadoInicial = await db.EstadosEnvio.FirstOrDefaultAsync(
            x => x.Codigo == codigoInicial && x.Activo,
            cancellationToken);
        if (estadoInicial is null)
            return Result.Failure("El estado inicial del flujo no se encuentra configurado.", ErrorType.Conflict);

        var equiposFueraDelNuevoOrigen = await db.EnvioEquipos
            .AnyAsync(
                x => x.EnvioId == envio.EnvioId &&
                     x.Equipo.UbicacionActualId != request.UbicacionOrigenId,
                cancellationToken);
        if (equiposFueraDelNuevoOrigen)
            return Result.Failure(
                "No se puede cambiar el origen porque uno o más equipos no se encuentran en la nueva ubicación.",
                ErrorType.Conflict);

        var fecha = DateTime.UtcNow;
        var cambioFlujo = envio.Direccion != direccionResult.Value || envio.EstadoEnvioId != estadoInicial.EstadoEnvioId;
        envio.UbicacionOrigenId = request.UbicacionOrigenId;
        envio.UbicacionDestinoId = request.UbicacionDestinoId;
        envio.Direccion = direccionResult.Value;
        envio.EstadoEnvioId = estadoInicial.EstadoEnvioId;
        envio.Observaciones = NormalizarOpcional(request.Observaciones);
        envio.FechaModificacion = fecha;
        envio.UsuarioModificacionId = usuarioId;

        if (cambioFlujo)
        {
            db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
            {
                EnvioId = envio.EnvioId,
                EstadoEnvioId = estadoInicial.EstadoEnvioId,
                UbicacionId = envio.UbicacionOrigenId,
                UsuarioId = usuarioId,
                Fecha = fecha,
                Observaciones = "Dirección del envío actualizada antes del despacho.",
                FechaCreacion = fecha,
                UsuarioCreacionId = usuarioId
            });
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                "El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.",
                ErrorType.Conflict);
        }
        return Result.Success();
    }

    private static Result<DireccionEnvioEnum> DeterminarDireccion(Ubicacion origen, Ubicacion destino)
    {
        if (origen.Tipo == TipoUbicacionEnum.Filial && destino.Tipo == TipoUbicacionEnum.Tecnologia)
            return Result<DireccionEnvioEnum>.Success(DireccionEnvioEnum.HaciaTecnologia);

        if (origen.Tipo == TipoUbicacionEnum.Tecnologia && destino.Tipo == TipoUbicacionEnum.Filial)
            return Result<DireccionEnvioEnum>.Success(DireccionEnvioEnum.HaciaFilial);

        return Result<DireccionEnvioEnum>.Failure(
            "El envío debe realizarse entre una filial y Tecnología.", ErrorType.Validation);
    }

    private static EnvioResponse ToResponse(Envio envio) => new(
        envio.EnvioId,
        envio.NumeroEnvio,
        envio.UbicacionOrigenId,
        envio.UbicacionDestinoId,
        envio.EstadoEnvioId,
        envio.Direccion,
        envio.UsuarioSolicitanteId,
        envio.Observaciones,
        envio.Transporte?.TipoTransporteId,
        envio.Transporte?.TipoTransporte.Estrategia,
        envio.Transporte?.TipoTransporte.Nombre);

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static bool EsEstadoEditable(string codigo) =>
        codigo == EstadoEnvioCodigos.EnFilial;
}
