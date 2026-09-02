using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Transportes;

public sealed class TransporteService(
    SistemaEnviosDbContext db, IUnitOfWork unitOfWork,
    IValidator<CrearTransporteRequest> validator,
    IValidator<ActualizarTransporteRequest> actualizarValidator,
    IUserContext userContext,
    IAlcanceEnvios alcance) : ITransporteService
{
    public async Task<Result<int>> CrearAsync(CrearTransporteRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<int>.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result<int>.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var envio = await db.Envios.Include(x => x.EstadoEnvio).FirstOrDefaultAsync(x => x.EnvioId == request.EnvioId, cancellationToken);
        if (envio is null) return Result<int>.Failure("El envío no existe.", ErrorType.NotFound);
        var enAlcanceEnvio = await alcance.VerificarAsync(envio.EnvioId, cancellationToken);
        if (enAlcanceEnvio.IsFailure) return Result<int>.Failure(enAlcanceEnvio.Error!, enAlcanceEnvio.ErrorType);
        if (!EsEstadoEditable(envio.EstadoEnvio.Codigo)) return Result<int>.Failure("El envío ya fue despachado y no admite cambios en el transporte.", ErrorType.Conflict);
        if (await db.Transportes.AnyAsync(x => x.EnvioId == request.EnvioId, cancellationToken)) return Result<int>.Failure("El envío ya tiene transporte registrado.", ErrorType.Conflict);
        var tipo = await db.TiposTransporte.FirstOrDefaultAsync(x => x.TipoTransporteId == request.TipoTransporteId && x.Activo, cancellationToken);
        if (tipo is null) return Result<int>.Failure("El tipo de transporte no existe o está inactivo.", ErrorType.Validation);
        if (tipo.Estrategia == EstrategiaTransporteEnum.EntregaDirectaTecnologia && envio.Direccion != DireccionEnvioEnum.HaciaTecnologia) return Result<int>.Failure("Los envíos de Tecnología a filial solo utilizan transporte interno.", ErrorType.Validation);
        var detalle = await ValidarDetalleAsync(tipo.Estrategia, request.ChoferInternoId, request.NombreResponsable, request.Parentesco, request.CedulaResponsable, request.PlacaVehiculo, cancellationToken);
        if (detalle.IsFailure) return Result<int>.Failure(detalle.Error!, detalle.ErrorType);

        var fecha = DateTime.UtcNow;
        var transporte = new Transporte { EnvioId = request.EnvioId, TipoTransporteId = tipo.TipoTransporteId, Observaciones = NormalizarOpcional(request.Observaciones), FechaCreacion = fecha, UsuarioCreacionId = usuarioId };
        db.Transportes.Add(transporte);
        if (tipo.Estrategia == EstrategiaTransporteEnum.TransportacionInstitucional)
        {
            var chofer = await db.ChoferesInternos.SingleAsync(x => x.ChoferInternoId == request.ChoferInternoId, cancellationToken);
            transporte.Interno = new TransporteInterno { ChoferInternoId = chofer.ChoferInternoId, NombreChoferAlMomento = chofer.NombreCompleto, NumeroEmpleadoAlMomento = chofer.NumeroEmpleado };
        }
        else
        {
            transporte.Privado = new TransportePrivado { NombreResponsable = request.NombreResponsable!.Trim(), Parentesco = request.Parentesco!.Trim(), CedulaResponsable = SoloDigitos(request.CedulaResponsable!), PlacaVehiculo = NormalizarMayuscula(request.PlacaVehiculo)! };
        }
        MarcarEnvioModificado(envio, usuarioId);
        if (envio.Direccion == DireccionEnvioEnum.HaciaFilial && envio.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnTransportacion)
        {
            var asignado = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.Codigo == EstadoEnvioCodigos.TransporteAsignado && x.Activo, cancellationToken);
            if (asignado is null || !await db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == envio.EstadoEnvioId && x.EstadoDestinoId == asignado.EstadoEnvioId && x.Activo, cancellationToken)) return Result<int>.Failure("El estado de transporte asignado no está configurado.", ErrorType.Conflict);
            envio.EstadoEnvioId = asignado.EstadoEnvioId;
            db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio { EnvioId = envio.EnvioId, EstadoEnvioId = asignado.EstadoEnvioId, UbicacionId = envio.UbicacionOrigenId, UsuarioId = usuarioId, Fecha = DateTime.UtcNow, Observaciones = "Chofer asignado por Transportación.", FechaCreacion = DateTime.UtcNow, UsuarioCreacionId = usuarioId });
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(transporte.TransporteId);
    }

    public async Task<Result<TransporteResponse>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default)
    {
        var enAlcance = await alcance.VerificarAsync(envioId, cancellationToken);
        if (enAlcance.IsFailure) return Result<TransporteResponse>.Failure(enAlcance.Error!, enAlcance.ErrorType);
        var x = await db.Transportes.AsNoTracking().Include(t => t.TipoTransporte).Include(t => t.Interno).Include(t => t.Privado).FirstOrDefaultAsync(t => t.EnvioId == envioId, cancellationToken);
        return x is null ? Result<TransporteResponse>.Failure("El transporte no existe.", ErrorType.NotFound) : Result<TransporteResponse>.Success(Mapear(x));
    }

    public async Task<Result> ConfirmarAsync(int transporteId, CancellationToken cancellationToken = default)
    {
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var transporte = await db.Transportes.Include(x => x.TipoTransporte).Include(x => x.Interno).Include(x => x.Envio).ThenInclude(x => x.EstadoEnvio).FirstOrDefaultAsync(x => x.TransporteId == transporteId, cancellationToken);
        if (transporte is null) return Result.Failure("El transporte no existe.", ErrorType.NotFound);
        var enAlcance = await alcance.VerificarAsync(transporte.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;
        if (transporte.TipoTransporte.Estrategia != EstrategiaTransporteEnum.TransportacionInstitucional || transporte.Interno is null) return Result.Failure("Los transportes privados no requieren confirmación de Transportación.", ErrorType.Conflict);
        var interno = transporte.Interno;
        if (interno.EntregaConfirmada) return Result.Failure("La entrega ya fue confirmada.", ErrorType.Conflict);
        // Transportación toma custodia y saca el envío a ruta en un solo paso: los dos estados
        // intermedios de confirmación ya no forman parte del flujo interno.
        if (interno.FechaEntregaTransportacion is null || transporte.Envio.EstadoEnvio.Codigo != EstadoEnvioCodigos.EntregadoATransportacion) return Result.Failure("El envío no fue entregado a Transportación.", ErrorType.Conflict);
        var transito = await db.EstadosEnvio.FirstOrDefaultAsync(x => x.Codigo == EstadoEnvioCodigos.EnTransito && x.Activo, cancellationToken);
        if (transito is null) return Result.Failure("El estado En tránsito no está configurado.", ErrorType.Conflict);
        if (!await TransicionExiste(transporte.Envio.EstadoEnvioId, transito.EstadoEnvioId, cancellationToken)) return Result.Failure("La transición de entrega a tránsito no está configurada.", ErrorType.Conflict);
        var fecha = DateTime.UtcNow;
        interno.EntregaConfirmada = true; interno.FechaConfirmacionEntrega = fecha; interno.UsuarioConfirmacionId = usuarioId;
        transporte.Envio.EstadoEnvioId = transito.EstadoEnvioId; transporte.Envio.FechaModificacion = fecha; transporte.Envio.UsuarioModificacionId = usuarioId;
        AgregarHistorial(transporte.Envio, transito.EstadoEnvioId, usuarioId, fecha, "Transportación tomó custodia y puso el envío en tránsito.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ActualizarAsync(ActualizarTransporteRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await actualizarValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result.Failure(validation.ToErrorMessage(), ErrorType.Validation);
        if (userContext.UserId is not Guid usuarioId) return Result.Failure("No fue posible identificar al usuario autenticado.", ErrorType.Unauthorized);
        var transporte = await db.Transportes.Include(x => x.Envio).ThenInclude(x => x.EstadoEnvio).Include(x => x.Interno).Include(x => x.Privado).FirstOrDefaultAsync(x => x.TransporteId == request.TransporteId, cancellationToken);
        if (transporte is null) return Result.Failure("El transporte no existe.", ErrorType.NotFound);
        var enAlcance = await alcance.VerificarAsync(transporte.EnvioId, cancellationToken);
        if (enAlcance.IsFailure) return enAlcance;
        if (!EsEstadoEditable(transporte.Envio.EstadoEnvio.Codigo)) return Result.Failure("El envío ya fue despachado y no admite cambios en el transporte.", ErrorType.Conflict);
        var tipo = await db.TiposTransporte.FirstOrDefaultAsync(x => x.TipoTransporteId == request.TipoTransporteId && x.Activo, cancellationToken);
        if (tipo is null) return Result.Failure("El tipo de transporte no existe o está inactivo.", ErrorType.Validation);
        if (tipo.Estrategia == EstrategiaTransporteEnum.EntregaDirectaTecnologia && transporte.Envio.Direccion != DireccionEnvioEnum.HaciaTecnologia) return Result.Failure("Los envíos de Tecnología a filial solo utilizan transporte interno.", ErrorType.Validation);
        var detalle = await ValidarDetalleAsync(tipo.Estrategia, request.ChoferInternoId, request.NombreResponsable, request.Parentesco, request.CedulaResponsable, request.PlacaVehiculo, cancellationToken);
        if (detalle.IsFailure) return detalle;
        if (transporte.Interno is not null) db.TransportesInternos.Remove(transporte.Interno);
        if (transporte.Privado is not null) db.TransportesPrivados.Remove(transporte.Privado);
        transporte.TipoTransporteId = tipo.TipoTransporteId; transporte.Observaciones = NormalizarOpcional(request.Observaciones); transporte.FechaModificacion = DateTime.UtcNow; transporte.UsuarioModificacionId = usuarioId;
        if (tipo.Estrategia == EstrategiaTransporteEnum.TransportacionInstitucional)
        {
            var chofer = await db.ChoferesInternos.SingleAsync(x => x.ChoferInternoId == request.ChoferInternoId, cancellationToken);
            transporte.Interno = new TransporteInterno { ChoferInternoId = chofer.ChoferInternoId, NombreChoferAlMomento = chofer.NombreCompleto, NumeroEmpleadoAlMomento = chofer.NumeroEmpleado };
        }
        else transporte.Privado = new TransportePrivado { NombreResponsable = request.NombreResponsable!.Trim(), Parentesco = request.Parentesco!.Trim(), CedulaResponsable = SoloDigitos(request.CedulaResponsable!), PlacaVehiculo = NormalizarMayuscula(request.PlacaVehiculo)! };
        await unitOfWork.SaveChangesAsync(cancellationToken); return Result.Success();
    }

    private async Task<Result> ValidarDetalleAsync(EstrategiaTransporteEnum estrategia, int? choferId, string? nombre, string? parentesco, string? cedula, string? placa, CancellationToken ct)
    {
        if (estrategia == EstrategiaTransporteEnum.TransportacionInstitucional)
        {
            if (choferId is null || !await db.ChoferesInternos.AnyAsync(x => x.ChoferInternoId == choferId && x.Activo, ct)) return Result.Failure("Debe seleccionar un chofer interno activo.", ErrorType.Validation);
            if (!string.IsNullOrWhiteSpace(nombre) || !string.IsNullOrWhiteSpace(parentesco) || !string.IsNullOrWhiteSpace(cedula) || !string.IsNullOrWhiteSpace(placa)) return Result.Failure("Un transporte interno no admite datos del transporte privado.", ErrorType.Validation);
        }
        else
        {
            if (choferId is not null) return Result.Failure("Un transporte privado no admite chofer interno.", ErrorType.Validation);
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(parentesco) || SoloDigitos(cedula ?? "").Length != 11 || string.IsNullOrWhiteSpace(placa)) return Result.Failure("Nombre, parentesco, cédula y placa son obligatorios para el transporte privado.", ErrorType.Validation);
        }
        return Result.Success();
    }

    private Task<bool> TransicionExiste(int origen, int destino, CancellationToken ct) => db.TransicionesEstadoEnvio.AnyAsync(x => x.EstadoOrigenId == origen && x.EstadoDestinoId == destino && x.Activo, ct);
    private void AgregarHistorial(Envio envio, int estadoId, Guid usuario, DateTime fecha, string obs) => db.HistorialEstadosEnvio.Add(new HistorialEstadoEnvio { EnvioId = envio.EnvioId, EstadoEnvioId = estadoId, UbicacionId = envio.UbicacionOrigenId, UsuarioId = usuario, Fecha = fecha, Observaciones = obs, FechaCreacion = fecha, UsuarioCreacionId = usuario });
    private static TransporteResponse Mapear(Transporte x) => new(x.TransporteId, x.EnvioId, x.TipoTransporteId, x.TipoTransporte.Codigo, x.TipoTransporte.Nombre, x.TipoTransporte.Estrategia, x.Interno?.ChoferInternoId, x.Interno?.NombreChoferAlMomento, x.Interno?.NumeroEmpleadoAlMomento, x.Privado?.NombreResponsable, x.Privado?.Parentesco, x.Privado is null ? null : $"*******{x.Privado.CedulaResponsable[^4..]}", x.Privado?.PlacaVehiculo, x.Interno?.FechaEntregaTransportacion ?? x.Privado?.FechaEntrega, x.Observaciones, x.Interno?.EntregaConfirmada ?? false, x.Interno?.FechaConfirmacionEntrega, x.Interno?.UsuarioConfirmacionId);
    private static bool EsEstadoEditable(string codigo) => codigo is EstadoEnvioCodigos.EnFilial or EstadoEnvioCodigos.EnPreparacionTecnologia or EstadoEnvioCodigos.DespachadoPorTecnologia;
    private static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    private static string? NormalizarMayuscula(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();
    private static string SoloDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
    private static void MarcarEnvioModificado(Envio envio, Guid usuarioId) { envio.FechaModificacion = DateTime.UtcNow; envio.UsuarioModificacionId = usuarioId; }
}
