namespace SistemaEnvios.Domain.Entities;

public sealed class TransportePrivado
{
    public int TransporteId { get; set; }
    public string NombreResponsable { get; set; } = null!;
    public string Parentesco { get; set; } = null!;
    public string CedulaResponsable { get; set; } = null!;
    public string PlacaVehiculo { get; set; } = null!;
    public DateTime? FechaEntrega { get; set; }
    public Guid? UsuarioQueEntregoId { get; set; }
    public Transporte Transporte { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
}
