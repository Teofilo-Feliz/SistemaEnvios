namespace SistemaEnvios.Domain.Entities;

public sealed class TransporteInterno
{
    public int TransporteId { get; set; }
    public int ChoferInternoId { get; set; }
    public string NombreChoferAlMomento { get; set; } = null!;
    public string NumeroEmpleadoAlMomento { get; set; } = null!;
    public DateTime? FechaEntregaTransportacion { get; set; }
    public bool EntregaConfirmada { get; set; }
    public DateTime? FechaConfirmacionEntrega { get; set; }
    public Guid? UsuarioConfirmacionId { get; set; }
    public Transporte Transporte { get; set; } = null!;
    public ChoferInterno ChoferInterno { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
}
