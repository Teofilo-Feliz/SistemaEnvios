using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class TransicionEstadoEnvioService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IValidator<TransicionEstadoEnvio> validator) : ITransicionEstadoEnvioService
{
    public async Task<Result<TransicionEstadoEnvio>> ObtenerAsync(int transicionId, CancellationToken cancellationToken = default)
    {
        var transicion = await db.TransicionesEstadoEnvio.AsNoTracking().FirstOrDefaultAsync(x => x.TransicionEstadoEnvioId == transicionId, cancellationToken);
        return transicion is null ? Result<TransicionEstadoEnvio>.Failure("La transición no existe.") : Result<TransicionEstadoEnvio>.Success(transicion);
    }

    public async Task<Result<IReadOnlyCollection<TransicionEstadoEnvio>>> ListarAsync(bool soloActivas = true, CancellationToken cancellationToken = default)
    {
        var query = db.TransicionesEstadoEnvio.AsNoTracking();
        if (soloActivas) query = query.Where(x => x.Activo);
        var transiciones = await query.OrderBy(x => x.EstadoOrigenId).ThenBy(x => x.EstadoDestinoId).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<TransicionEstadoEnvio>>.Success(transiciones);
    }

    public async Task<Result<int>> CrearAsync(TransicionEstadoEnvio transicion, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(transicion, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage());

        var estadosExistentes = await db.EstadosEnvio.CountAsync(
            x => (x.EstadoEnvioId == transicion.EstadoOrigenId || x.EstadoEnvioId == transicion.EstadoDestinoId) && x.Activo,
            cancellationToken);
        if (estadosExistentes != 2)
            return Result<int>.Failure("El estado de origen o destino no existe o está inactivo.");

        if (await db.TransicionesEstadoEnvio.AnyAsync(
                x => x.EstadoOrigenId == transicion.EstadoOrigenId && x.EstadoDestinoId == transicion.EstadoDestinoId,
                cancellationToken))
            return Result<int>.Failure("La transición entre los estados indicados ya existe.");

        db.TransicionesEstadoEnvio.Add(transicion);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(transicion.TransicionEstadoEnvioId);
    }

    public async Task<Result> CambiarActivoAsync(int transicionId, bool activo, CancellationToken cancellationToken = default)
    {
        var transicion = await db.TransicionesEstadoEnvio.FindAsync([transicionId], cancellationToken);
        if (transicion is null) return Result.Failure("La transición no existe.");
        if (transicion.Activo == activo) return Result.Success();
        transicion.Activo = activo;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
