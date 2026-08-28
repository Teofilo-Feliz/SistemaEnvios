namespace SistemaEnvios.Domain.Entities;

public class TransicionEstadoEnvio
{
    public int TransicionEstadoEnvioId { get; set; }
    public int EstadoOrigenId { get; set; }
    public int EstadoDestinoId { get; set; }
    public bool Activo { get; set; }

    public EstadoEnvio EstadoOrigen { get; set; } = null!;
    public EstadoEnvio EstadoDestino { get; set; } = null!;
}
