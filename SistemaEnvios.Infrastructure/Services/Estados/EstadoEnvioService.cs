using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Estados;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Estados;

public sealed class EstadoEnvioService(
    SistemaEnviosDbContext db,
    IUnitOfWork unitOfWork,
    IUserContext userContext) : IEstadoEnvioService
{
    public async Task<Result<EstadoEnvioResponse>> ObtenerAsync(int estadoId, CancellationToken cancellationToken = default)
    {
        var estado = await db.EstadosEnvio.AsNoTracking().Where(x => x.EstadoEnvioId == estadoId)
            .Select(x => new EstadoEnvioResponse(x.EstadoEnvioId, x.Codigo, x.Nombre, x.Descripcion, x.EsFinal, x.Activo))
            .FirstOrDefaultAsync(cancellationToken);
        return estado is null ? Result<EstadoEnvioResponse>.Failure("El estado no existe.", ErrorType.NotFound) : Result<EstadoEnvioResponse>.Success(estado);
    }

    public async Task<Result<IReadOnlyCollection<EstadoEnvioResponse>>> ListarAsync(bool soloActivos = true, CancellationToken cancellationToken = default)
    {
        var query = db.EstadosEnvio.AsNoTracking();
        if (soloActivos) query = query.Where(x => x.Activo);
        var estados = await query.OrderBy(x => x.Nombre)
            .Select(x => new EstadoEnvioResponse(x.EstadoEnvioId, x.Codigo, x.Nombre, x.Descripcion, x.EsFinal, x.Activo))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyCollection<EstadoEnvioResponse>>.Success(estados);
    }

    public async Task<Result> CambiarAsync(CambiarEstadoEnvioRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EnvioId <= 0 || request.EstadoDestinoId <= 0)
            return Result.Failure("El envío y el estado destino son requeridos.", ErrorType.Validation);
        if (request.Observaciones?.Length > 2000)
            return Result.Failure("Las observaciones no pueden exceder los 2000 caracteres.", ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var envio = await db.Envios.Include(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);
        if (envio is null) return Result.Failure("El envío no existe.", ErrorType.NotFound);
        if (envio.EstadoEnvio.EsFinal) return Result.Failure("El envío está finalizado y no admite cambios.", ErrorType.Conflict);
        var destino = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.EstadoEnvioId == request.EstadoDestinoId && x.Activo, cancellationToken);
        if (destino is null) return Result.Failure("El estado no existe o está inactivo.", ErrorType.NotFound);
        if (envio.EstadoEnvioId == destino.EstadoEnvioId) return Result.Failure("El envío ya se encuentra en el estado indicado.", ErrorType.Conflict);
        if (!PerteneceAlFlujo(envio.Direccion, destino.Codigo)) return Result.Failure("El estado indicado no pertenece a la dirección de este envío.", ErrorType.Validation);
        if (destino.Codigo == EstadoEnvioCodigos.ConfirmadoPorTransportacion)
            return Result.Failure("La confirmación debe realizarse mediante el servicio de transporte.", ErrorType.Conflict);
        if (!await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == destino.EstadoEnvioId && x.Activo, cancellationToken))
            return Result.Failure("La transición de estado no está permitida.", ErrorType.Conflict);
        var requisitos = await ValidarRequisitosAsync(envio.EnvioId, destino.Codigo, cancellationToken);
        if (requisitos.IsFailure) return requisitos;

        AplicarCambio(envio, destino, usuarioId, request.Observaciones);
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

    private void AplicarCambio(Envio envio, EstadoEnvio destino, Guid usuarioId, string? observaciones)
    {
        var fecha = DateTime.UtcNow;
        envio.EstadoEnvioId = destino.EstadoEnvioId;
        envio.FechaFinalizacion = destino.EsFinal ? fecha : null;
        envio.FechaModificacion = fecha;
        envio.UsuarioModificacionId = usuarioId;
        db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio
        {
            EnvioId = envio.EnvioId,
            EstadoEnvioId = destino.EstadoEnvioId,
            UbicacionId = DeterminarUbicacion(envio, destino.Codigo),
            UsuarioId = usuarioId,
            Fecha = fecha,
            Observaciones = NormalizarOpcional(observaciones),
            FechaCreacion = fecha,
            UsuarioCreacionId = usuarioId
        });
    }

    private async Task<Result> ValidarRequisitosAsync(int envioId, string codigoDestino, CancellationToken cancellationToken)
    {
        if (codigoDestino is EstadoEnvioCodigos.EntregadoATransportacion or EstadoEnvioCodigos.DespachadoPorTecnologia)
        {
            if (!await db.EnvioEquipos.AnyAsync(x => x.EnvioId == envioId, cancellationToken)) return Result.Failure("No se puede despachar un envío sin equipos.", ErrorType.Conflict);
            var transporte = await db.Transportes.FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
            if (transporte is null) return Result.Failure("No se puede despachar un envío sin transporte registrado.", ErrorType.Conflict);
            transporte.FechaEntregaTransportacion ??= DateTime.UtcNow;
        }
        if (codigoDestino is EstadoEnvioCodigos.RecibidoPorTecnologia or EstadoEnvioCodigos.RecepcionValidadaEnFilial)
            return Result.Failure("El estado final solamente puede establecerse completando la recepción.", ErrorType.Conflict);
        return Result.Success();
    }

    private static int DeterminarUbicacion(Envio envio, string codigoDestino) =>
        codigoDestino is EstadoEnvioCodigos.RecibidoPorTransportacion or EstadoEnvioCodigos.EnEsperaDeTecnologia or
            EstadoEnvioCodigos.EnProcesoDeRevision or EstadoEnvioCodigos.RecibidoPorTecnologia or
            EstadoEnvioCodigos.PendienteRecepcionFilial or EstadoEnvioCodigos.RecibidoEnFilial or
            EstadoEnvioCodigos.RecepcionValidadaEnFilial
            ? envio.UbicacionDestinoId
            : envio.UbicacionOrigenId;

    private static bool PerteneceAlFlujo(DireccionEnvioEnum direccion, string codigo) => direccion switch
    {
        DireccionEnvioEnum.HaciaTecnologia => codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EntregadoATransportacion or EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion or EstadoEnvioCodigos.ConfirmadoPorTransportacion or EstadoEnvioCodigos.EnTransito or EstadoEnvioCodigos.RecibidoPorTransportacion or EstadoEnvioCodigos.EnEsperaDeTecnologia or EstadoEnvioCodigos.EnProcesoDeRevision or EstadoEnvioCodigos.RecibidoPorTecnologia or EstadoEnvioCodigos.IncidenciaEnTransportacion,
        DireccionEnvioEnum.HaciaFilial => codigo is EstadoEnvioCodigos.EnPreparacionTecnologia or EstadoEnvioCodigos.DespachadoPorTecnologia or EstadoEnvioCodigos.EnTransito or EstadoEnvioCodigos.PendienteRecepcionFilial or EstadoEnvioCodigos.RecibidoEnFilial or EstadoEnvioCodigos.RecepcionValidadaEnFilial or EstadoEnvioCodigos.IncidenciaEnTransportacion,
        _ => false
    };

    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
