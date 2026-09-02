using Microsoft.EntityFrameworkCore;
using FluentValidation;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Incidencias;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Application.Interfaces.Security;

namespace SistemaEnvios.Infrastructure.Services.Incidencias;

public sealed class IncidenciaService : IIncidenciaService
{
    private readonly SistemaEnviosDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CrearIncidenciaRequest> _validator;
    private readonly IUserContext _userContext;
    private readonly IAlcanceEnvios _alcance;

    public IncidenciaService(
        SistemaEnviosDbContext db,
        IUnitOfWork unitOfWork,
        IValidator<CrearIncidenciaRequest> validator,
        IUserContext userContext,
        IAlcanceEnvios alcance)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _userContext = userContext;
        _alcance = alcance;
    }

    public async Task<Result<int>> RegistrarAsync(CrearIncidenciaRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        }

        if (_userContext.UserId is not Guid usuarioId)
            return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var envio = await _db.Envios
            .Include(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);

        if (envio is null)
        {
            return Result<int>.Failure("El envío no existe.", ErrorType.NotFound);
        }

        var enAlcanceEnvio = await _alcance.VerificarAsync(envio.EnvioId, cancellationToken);
        if (enAlcanceEnvio.IsFailure) return Result<int>.Failure(enAlcanceEnvio.Error!, enAlcanceEnvio.ErrorType);

        if (envio.EstadoEnvio.EsFinal)
            return Result<int>.Failure("El envío está finalizado y no admite nuevas incidencias.", ErrorType.Conflict);

        if (request.EnvioEquipoId.HasValue)
        {
            var equipoPerteneceAlEnvio = await _db.EnvioEquipos.AnyAsync(
                x => x.EnvioEquipoId == request.EnvioEquipoId && x.EnvioId == request.EnvioId,
                cancellationToken);

            if (!equipoPerteneceAlEnvio)
            {
                return Result<int>.Failure("El equipo no pertenece al envío.", ErrorType.Validation);
            }
        }

        if (request.TransporteId.HasValue)
        {
            var transportePerteneceAlEnvio = await _db.Transportes.AnyAsync(
                x => x.TransporteId == request.TransporteId && x.EnvioId == request.EnvioId,
                cancellationToken);

            if (!transportePerteneceAlEnvio)
            {
                return Result<int>.Failure("El transporte no pertenece al envío.", ErrorType.Validation);
            }
        }

        var incidencia = new Incidencia
        {
            EnvioId = request.EnvioId,
            EnvioEquipoId = request.EnvioEquipoId,
            TransporteId = request.TransporteId,
            Descripcion = request.Descripcion.Trim(),
            UsuarioCreacionId = usuarioId,
            FechaCreacion = DateTime.UtcNow
        };

        _db.Incidencias.Add(incidencia);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(incidencia.IncidenciaId);
    }

    public async Task<Result<IncidenciaResponse>> ObtenerAsync(int incidenciaId, CancellationToken cancellationToken = default)
    {
        var incidencia = await _db.Incidencias.AsNoTracking().Where(x => x.IncidenciaId == incidenciaId)
            .Select(x => new IncidenciaResponse(x.IncidenciaId, x.EnvioId, x.EnvioEquipoId, x.TransporteId,
                x.Descripcion, x.FechaCreacion, x.UsuarioCreacionId)).FirstOrDefaultAsync(cancellationToken);
        return incidencia is null ? Result<IncidenciaResponse>.Failure("La incidencia no existe.", ErrorType.NotFound) : Result<IncidenciaResponse>.Success(incidencia);
    }

    public async Task<Result<IReadOnlyCollection<IncidenciaResponse>>> ListarPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Envios.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result<IReadOnlyCollection<IncidenciaResponse>>.Failure("El envío no existe.", ErrorType.NotFound);

        var enAlcance = await _alcance.VerificarAsync(envioId, cancellationToken);
        if (enAlcance.IsFailure) return Result<IReadOnlyCollection<IncidenciaResponse>>.Failure(enAlcance.Error!, enAlcance.ErrorType);
        var incidencias = await _db.Incidencias.AsNoTracking().Where(x => x.EnvioId == envioId)
            .OrderByDescending(x => x.FechaCreacion)
            .Select(x => new IncidenciaResponse(x.IncidenciaId, x.EnvioId, x.EnvioEquipoId, x.TransporteId,
                x.Descripcion, x.FechaCreacion, x.UsuarioCreacionId)).ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<IncidenciaResponse>>.Success(incidencias);
    }
}
