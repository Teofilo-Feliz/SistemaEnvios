namespace SistemaEnvios.Domain.Entities;

public sealed class ChoferInterno : AuditoriaEntitie
{
    public int ChoferInternoId { get; set; }
    public string NombreCompleto { get; set; } = null!;
    public string NumeroEmpleado { get; set; } = null!;
    public bool Activo { get; set; }
    public ICollection<TransporteInterno> Transportes { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
