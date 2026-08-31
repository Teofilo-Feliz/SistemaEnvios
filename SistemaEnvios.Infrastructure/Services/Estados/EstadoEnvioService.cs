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

    public async Task<Result> EntregarATransportacionAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default)
    {
        if (envioId <= 0) return Result.Failure("El envío es requerido.", ErrorType.Validation);
        if (observaciones?.Length > 2000) return Result.Failure("Las observaciones no pueden exceder los 2000 caracteres.", ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId)
            return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);

        var envio = await db.Envios.Include(x => x.EstadoEnvio)
            .FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        if (envio is null) return Result.Failure("El envío no existe.", ErrorType.NotFound);
        if (envio.Direccion != DireccionEnvioEnum.HaciaTecnologia || envio.EstadoEnvio.Codigo != EstadoEnvioCodigos.EnFilial)
            return Result.Failure("El envío no está preparado para ser entregado a transportación.", ErrorType.Conflict);

        if (!await db.EnvioEquipos.AnyAsync(x => x.EnvioId == envioId, cancellationToken))
            return Result.Failure("No se puede entregar un envío sin equipos.", ErrorType.Conflict);
        var transporte = await db.Transportes.Include(x => x.TipoTransporte).Include(x => x.Interno)
            .FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        if (transporte is null) return Result.Failure("No se puede entregar un envío sin transporte registrado.", ErrorType.Conflict);
        if (transporte.TipoTransporte.Estrategia != EstrategiaTransporteEnum.TransportacionInstitucional || transporte.Interno is null)
            return Result.Failure("Este envío no utiliza transportación institucional.", ErrorType.Conflict);

        var estados = await db.EstadosEnvio.Where(x => x.Activo &&
                (x.Codigo == EstadoEnvioCodigos.EntregadoATransportacion || x.Codigo == EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion))
            .ToDictionaryAsync(x => x.Codigo, cancellationToken);
        if (!estados.TryGetValue(EstadoEnvioCodigos.EntregadoATransportacion, out var entregado) ||
            !estados.TryGetValue(EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion, out var pendiente))
            return Result.Failure("El flujo de confirmación de transportación no está configurado.", ErrorType.Conflict);
        if (!await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == entregado.EstadoEnvioId && x.Activo, cancellationToken) ||
            !await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == entregado.EstadoEnvioId && x.EstadoDestinoId == pendiente.EstadoEnvioId && x.Activo, cancellationToken))
            return Result.Failure("La transición de entrega a transportación no está configurada.", ErrorType.Conflict);

        transporte.Interno.FechaEntregaTransportacion ??= DateTime.UtcNow;
        AplicarCambio(envio, entregado, usuarioId, observaciones);
        AplicarCambio(envio, pendiente, usuarioId, "Envío entregado al chofer; pendiente de confirmación de transportación.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("El envío fue modificado por otra operación. Actualice los datos e intente nuevamente.", ErrorType.Conflict);
        }
        return Result.Success();
    }

    public async Task<Result> EntregarTransportePrivadoAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var envio = await db.Envios.Include(x => x.EstadoEnvio).FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        if (envio is null) return Result.Failure("El envío no existe.", ErrorType.NotFound);
        if (envio.Direccion != DireccionEnvioEnum.HaciaTecnologia || envio.EstadoEnvio.Codigo != EstadoEnvioCodigos.EnFilial) return Result.Failure("El envío no está preparado para salir de la filial.", ErrorType.Conflict);
        if (!await db.EnvioEquipos.AnyAsync(x => x.EnvioId == envioId, cancellationToken)) return Result.Failure("No se puede despachar un envío sin equipos.", ErrorType.Conflict);
        var transporte = await db.Transportes.Include(x => x.TipoTransporte).Include(x => x.Privado).FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        if (transporte?.TipoTransporte.Estrategia != EstrategiaTransporteEnum.EntregaDirectaTecnologia || transporte.Privado is null) return Result.Failure("El envío no tiene un transporte privado válido.", ErrorType.Conflict);
        var estados = await db.EstadosEnvio.Where(x => x.Activo && (x.Codigo == EstadoEnvioCodigos.DespachadoTransportePrivado || x.Codigo == EstadoEnvioCodigos.EnTransito)).ToDictionaryAsync(x => x.Codigo, cancellationToken);
        if (!estados.TryGetValue(EstadoEnvioCodigos.DespachadoTransportePrivado, out var despachado) || !estados.TryGetValue(EstadoEnvioCodigos.EnTransito, out var transito)) return Result.Failure("El flujo privado no está configurado.", ErrorType.Conflict);
        if (!await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == despachado.EstadoEnvioId && x.Activo, cancellationToken) || !await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == despachado.EstadoEnvioId && x.EstadoDestinoId == transito.EstadoEnvioId && x.Activo, cancellationToken)) return Result.Failure("Las transiciones del flujo privado no están configuradas.", ErrorType.Conflict);
        var fecha = DateTime.UtcNow; transporte.Privado.FechaEntrega ??= fecha; transporte.Privado.UsuarioQueEntregoId ??= usuarioId;
        AplicarCambio(envio, despachado, usuarioId, observaciones ?? "Entrega registrada al responsable privado.");
        AplicarCambio(envio, transito, usuarioId, "Envío privado puesto en tránsito hacia Tecnología.");
        await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success();
    }

    public async Task<Result> RegistrarLlegadaTecnologiaAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var envio = await db.Envios.Include(x => x.EstadoEnvio).Include(x => x.Transporte!).ThenInclude(x => x.TipoTransporte).FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
        if (envio is null) return Result.Failure("El envío no existe.", ErrorType.NotFound);
        if (envio.Direccion != DireccionEnvioEnum.HaciaTecnologia || envio.EstadoEnvio.Codigo != EstadoEnvioCodigos.EnTransito || envio.Transporte is null) return Result.Failure("El envío no está en tránsito hacia Tecnología.", ErrorType.Conflict);
        var espera = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.Codigo == EstadoEnvioCodigos.EnEsperaDeTecnologia && x.Activo, cancellationToken);
        if (espera is null) return Result.Failure("El estado de espera de Tecnología no está configurado.", ErrorType.Conflict);
        if (envio.Transporte.TipoTransporte.Estrategia == EstrategiaTransporteEnum.TransportacionInstitucional)
        {
            var recibido = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.Codigo == EstadoEnvioCodigos.RecibidoPorTransportacion && x.Activo, cancellationToken);
            if (recibido is null || !await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == recibido.EstadoEnvioId && x.Activo, cancellationToken) || !await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == recibido.EstadoEnvioId && x.EstadoDestinoId == espera.EstadoEnvioId && x.Activo, cancellationToken)) return Result.Failure("El flujo de llegada institucional no está configurado.", ErrorType.Conflict);
            AplicarCambio(envio, recibido, usuarioId, observaciones ?? "Envío recibido en el punto logístico.");
        }
        else if (!await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == espera.EstadoEnvioId && x.Activo, cancellationToken)) return Result.Failure("El flujo de llegada privada no está configurado.", ErrorType.Conflict);
        AplicarCambio(envio, espera, usuarioId, "Envío disponible para procesamiento de Tecnología.");
        db.Notificaciones.Add(new Notificacion { EnvioId=envio.EnvioId,Tipo="ENVIO_DISPONIBLE_TECNOLOGIA",Titulo="Envío disponible para retiro",Mensaje=$"El envío {envio.NumeroEnvio} fue recibido y está disponible para ser retirado de Transportación.",DestinatarioRol="TECNOLOGIA",FechaCreacion=DateTime.UtcNow });
        await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success();
    }

    public async Task<Result> ConfirmarLlegadaTransportacionAsync(int envioId, string? observaciones = null, CancellationToken cancellationToken = default)
    {
        var estrategia = await db.Transportes.Where(x=>x.EnvioId==envioId).Select(x=>(EstrategiaTransporteEnum?)x.TipoTransporte.Estrategia).FirstOrDefaultAsync(cancellationToken);
        if (estrategia != EstrategiaTransporteEnum.TransportacionInstitucional) return Result.Failure("Solo Transportación puede confirmar la llegada de envíos internos.",ErrorType.Conflict);
        return await RegistrarLlegadaTecnologiaAsync(envioId,observaciones,cancellationToken);
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
            var transporte = await db.Transportes.Include(x => x.Interno).FirstOrDefaultAsync(x => x.EnvioId == envioId, cancellationToken);
            if (transporte is null) return Result.Failure("No se puede despachar un envío sin transporte registrado.", ErrorType.Conflict);
            if (codigoDestino == EstadoEnvioCodigos.EntregadoATransportacion)
            {
                if (transporte.Interno is null) return Result.Failure("El envío no tiene transporte interno registrado.", ErrorType.Conflict);
                transporte.Interno.FechaEntregaTransportacion ??= DateTime.UtcNow;
            }
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
        DireccionEnvioEnum.HaciaTecnologia => codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EntregadoATransportacion or EstadoEnvioCodigos.DespachadoTransportePrivado or EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion or EstadoEnvioCodigos.ConfirmadoPorTransportacion or EstadoEnvioCodigos.EnTransito or EstadoEnvioCodigos.RecibidoPorTransportacion or EstadoEnvioCodigos.EnEsperaDeTecnologia or EstadoEnvioCodigos.EnProcesoDeRevision or EstadoEnvioCodigos.RecibidoPorTecnologia or EstadoEnvioCodigos.IncidenciaEnTransportacion,
        DireccionEnvioEnum.HaciaFilial => codigo is EstadoEnvioCodigos.EnPreparacionTecnologia or EstadoEnvioCodigos.DespachadoPorTecnologia or EstadoEnvioCodigos.EnTransito or EstadoEnvioCodigos.PendienteRecepcionFilial or EstadoEnvioCodigos.RecibidoEnFilial or EstadoEnvioCodigos.RecepcionValidadaEnFilial or EstadoEnvioCodigos.IncidenciaEnTransportacion,
        _ => false
    };

    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
