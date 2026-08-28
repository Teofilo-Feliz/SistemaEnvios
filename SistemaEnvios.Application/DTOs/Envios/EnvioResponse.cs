namespace SistemaEnvios.Application.DTOs.Envios;

using SistemaEnvios.Domain.Enums;

public sealed record EnvioResponse(
int EnvioId,
string NumeroEnvio,
int UbicacionOrigenId,
int UbicacionDestinoId,
int EstadoEnvioId,
DireccionEnvioEnum Direccion,
Guid UsuarioSolicitanteId,
string? Observaciones);
