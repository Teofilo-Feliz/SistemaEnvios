namespace SistemaEnvios.Application.DTOs.Envios;

public sealed record EnvioResponse(
int EnvioId,
string NumeroEnvio,
int UbicacionOrigenId,
int UbicacionDestinoId,
int EstadoEnvioId,
Guid UsuarioSolicitanteId,
string? Observaciones);
