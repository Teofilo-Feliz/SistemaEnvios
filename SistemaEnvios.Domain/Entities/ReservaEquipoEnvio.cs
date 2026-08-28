namespace SistemaEnvios.Domain.Entities;

public sealed class ReservaEquipoEnvio
{
    public int EquipoId { get; set; }
    public int EnvioId { get; set; }
    public DateTime FechaReserva { get; set; }
    public Guid UsuarioId { get; set; }

    public Equipo Equipo { get; set; } = null!;
    public Envio Envio { get; set; } = null!;
}
