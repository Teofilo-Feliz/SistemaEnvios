using SistemaEnvios.Application.Common;
using FluentValidation;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Envios;

public sealed class EnvioEquipoService(
    IEnvioEquipoRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<AgregarEquipoEnvioRequest> validator,
    SistemaEnviosDbContext db) : IEnvioEquipoService
{
    public async Task<Result<int>> AgregarAsync(AgregarEquipoEnvioRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<int>.Failure(validation.ToErrorMessage());
        }

        if (!await repository.ExisteEnvioAsync(request.EnvioId, cancellationToken) || !await repository.ExisteEquipoAsync(request.EquipoId, cancellationToken))
        {
            return Result<int>.Failure("El envío o equipo no existe.");
        }

        if (await repository.ExisteEnEnvioAsync(request.EnvioId, request.EquipoId, cancellationToken))
        {
            return Result<int>.Failure("El equipo ya pertenece a este envío.");
        }

        var envioEquipo = new EnvioEquipo
        {
            EnvioId = request.EnvioId,
            EquipoId = request.EquipoId,
            NumeroTicket = request.NumeroTicket.Trim(),
            UsuarioSolicitanteId = request.UsuarioSolicitanteId,
            Observaciones = request.Observaciones,
            FechaCreacion = DateTime.UtcNow
        };

        await repository.AgregarAsync(envioEquipo, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(envioEquipo.EnvioEquipoId);
    }

    public async Task<Result<IReadOnlyCollection<EnvioEquipo>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        if (!await db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<IReadOnlyCollection<EnvioEquipo>>.Failure("El envío no existe.");
        var equipos = await db.EnvioEquipos.AsNoTracking().Where(x => x.EnvioId == envioId).OrderBy(x => x.EnvioEquipoId).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<EnvioEquipo>>.Success(equipos);
    }
}
