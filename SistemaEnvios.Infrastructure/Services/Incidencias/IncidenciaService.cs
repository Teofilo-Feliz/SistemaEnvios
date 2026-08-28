using Microsoft.EntityFrameworkCore;
using FluentValidation;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Incidencias;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Incidencias;

public sealed class IncidenciaService : IIncidenciaService
{
    private readonly SistemaEnviosDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearIncidenciaRequest> _validator;

    public IncidenciaService(SistemaEnviosDbContext db, IUnitOfWork unitOfWork, IValidator<CrearIncidenciaRequest> validator)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<int>> RegistrarAsync(CrearIncidenciaRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<int>.Failure(validation.ToErrorMessage());
        }

        var envioExiste = await _db.Envios.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken);

        if (!envioExiste)
        {
            return Result<int>.Failure("El envío no existe.");
        }

        if (request.EnvioEquipoId.HasValue)
        {
            var equipoPerteneceAlEnvio = await _db.EnvioEquipos.AnyAsync(
                x => x.EnvioEquipoId == request.EnvioEquipoId && x.EnvioId == request.EnvioId,
                cancellationToken);

            if (!equipoPerteneceAlEnvio)
            {
                return Result<int>.Failure("El equipo no pertenece al envío.");
            }
        }

        if (request.TransporteId.HasValue)
        {
            var transportePerteneceAlEnvio = await _db.Transportes.AnyAsync(
                x => x.TransporteId == request.TransporteId && x.EnvioId == request.EnvioId,
                cancellationToken);

            if (!transportePerteneceAlEnvio)
            {
                return Result<int>.Failure("El transporte no pertenece al envío.");
            }
        }

        var incidencia = new Incidencia
        {
            EnvioId = request.EnvioId,
            EnvioEquipoId = request.EnvioEquipoId,
            TransporteId = request.TransporteId,
            Descripcion = request.Descripcion,
            UsuarioCreacionId = request.UsuarioId,
            FechaCreacion = DateTime.UtcNow
        };

        _db.Incidencias.Add(incidencia);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(incidencia.IncidenciaId);
    }

    public async Task<Result<Incidencia>> ObtenerAsync(int incidenciaId, CancellationToken cancellationToken = default)
    {
        var incidencia = await _db.Incidencias.AsNoTracking().FirstOrDefaultAsync(x => x.IncidenciaId == incidenciaId, cancellationToken);
        return incidencia is null ? Result<Incidencia>.Failure("La incidencia no existe.") : Result<Incidencia>.Success(incidencia);
    }

    public async Task<Result<IReadOnlyCollection<Incidencia>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<IReadOnlyCollection<Incidencia>>.Failure("El envío no existe.");
        var incidencias = await _db.Incidencias.AsNoTracking().Where(x => x.EnvioId == envioId).OrderByDescending(x => x.FechaCreacion).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<Incidencia>>.Success(incidencias);
    }
}
