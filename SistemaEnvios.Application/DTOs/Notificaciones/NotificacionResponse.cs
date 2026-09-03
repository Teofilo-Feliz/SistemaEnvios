namespace SistemaEnvios.Application.DTOs.Notificaciones;
public sealed record NotificacionResponse(long NotificacionId,int EnvioId,string NumeroEnvio,string Tipo,string Titulo,string Mensaje,string DestinatarioRol,DateTime FechaCreacion);
