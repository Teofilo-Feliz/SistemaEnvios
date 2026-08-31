namespace SistemaEnvios.Domain.Entities;
public sealed class Notificacion
{
    public long NotificacionId { get; set; }
    public int EnvioId { get; set; }
    public string Tipo { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string Mensaje { get; set; } = null!;
    public string DestinatarioRol { get; set; } = null!;
    public Guid? DestinatarioUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaLeida { get; set; }
    public Guid? UsuarioLecturaId { get; set; }
    public Envio Envio { get; set; } = null!;
}
