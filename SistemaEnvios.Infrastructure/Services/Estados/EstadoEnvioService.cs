using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class EstadoEnvioService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<EstadoEnvio> validator) : IEstadoEnvioService
{
    public async Task<Result<EstadoEnvio>> ObtenerAsync(int estadoId, CancellationToken cancellationToken = default)
    {
        var estado = await db.EstadosEnvio.AsNoTracking().FirstOrDefaultAsync(x => x.EstadoEnvioId == estadoId, cancellationToken);
        return estado is null ? Result<EstadoEnvio>.Failure("El estado no existe.") : Result<EstadoEnvio>.Success(estado);
    }

    public async Task<Result<IReadOnlyCollection<EstadoEnvio>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default)
    {
        var query = db.EstadosEnvio.AsNoTracking();
        if (soloActivos) query = query.Where(x => x.Activo);
        var estados = await query.OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<EstadoEnvio>>.Success(estados);
    }

    public async Task<Result<int>> CrearAsync(EstadoEnvio estado, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(estado, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage());

        var codigo = estado.Codigo.Trim();
        if (await db.EstadosEnvio.AnyAsync(x => x.Codigo == codigo, cancellationToken))
            return Result<int>.Failure("Ya existe un estado con el código indicado.");

        estado.Codigo = codigo;
        estado.Nombre = estado.Nombre.Trim();
        estado.Descripcion = string.IsNullOrWhiteSpace(estado.Descripcion) ? null : estado.Descripcion.Trim();
        estado.FechaCreacion = DateTime.UtcNow;
        db.EstadosEnvio.Add(estado);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(estado.EstadoEnvioId);
    }

    public async Task<Result> ActualizarAsync(EstadoEnvio estado, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(estado, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage());
        var actual = await db.EstadosEnvio.FindAsync([estado.EstadoEnvioId], cancellationToken);
        if (actual is null) return Result.Failure("El estado no existe.");
        var codigo = estado.Codigo.Trim();
        if (await db.EstadosEnvio.AnyAsync(x => x.EstadoEnvioId != estado.EstadoEnvioId && x.Codigo == codigo, cancellationToken))
            return Result.Failure("Ya existe un estado con el código indicado.");
        actual.Codigo = codigo;
        actual.Nombre = estado.Nombre.Trim();
        actual.Descripcion = string.IsNullOrWhiteSpace(estado.Descripcion) ? null : estado.Descripcion.Trim();
        actual.EsFinal = estado.EsFinal;
        actual.FechaModificacion = DateTime.UtcNow;
        actual.UsuarioModificacionId = estado.UsuarioModificacionId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarActivoAsync(int estadoId, bool activo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty) return Result.Failure("El usuario es requerido.");
        var estado = await db.EstadosEnvio.FindAsync([estadoId], cancellationToken);
        if (estado is null) return Result.Failure("El estado no existe.");
        if (estado.Activo == activo) return Result.Success();
        estado.Activo = activo;
        estado.FechaModificacion = DateTime.UtcNow;
        estado.UsuarioModificacionId = usuarioId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> CambiarAsync(int envioId, int destinoId, Guid usuarioId, int ubicacionId, string? observaciones = null, CancellationToken cancellationToken = default)
    {
        var envio = await db.Envios.FindAsync([envioId], cancellationToken);
        if (envio is null) return Result.Failure("El envío no existe.");
        if (usuarioId == Guid.Empty) return Result.Failure("El usuario es requerido.");
        if (observaciones?.Length > 2000) return Result.Failure("Las observaciones no pueden exceder los 2000 caracteres.");
        if (!await db.Ubicaciones.AnyAsync(x => x.UbicacionId == ubicacionId && x.Activo, cancellationToken))
            return Result.Failure("La ubicación no existe o está inactiva.");
        var estadoDestino = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.EstadoEnvioId == destinoId && x.Activo, cancellationToken);
        if (estadoDestino is null) return Result.Failure("El estado no existe o está inactivo.");
        if (envio.EstadoEnvioId == destinoId) return Result.Failure("El envío ya se encuentra en el estado indicado.");
        if (!await db.TransicionesEstadoEnvio.AnyAsync(
                x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == destinoId && x.Activo,
                cancellationToken))
            return Result.Failure("La transición de estado no está permitida.");
        envio.EstadoEnvioId = destinoId;
        envio.FechaFinalizacion = estadoDestino.EsFinal ? DateTime.UtcNow : null;
        envio.FechaModificacion = DateTime.UtcNow;
        envio.UsuarioModificacionId = usuarioId;
        db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio { EnvioId = envioId, EstadoEnvioId = destinoId, UbicacionId = ubicacionId, UsuarioId = usuarioId, Fecha = DateTime.UtcNow, Observaciones = observaciones, FechaCreacion = DateTime.UtcNow });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
