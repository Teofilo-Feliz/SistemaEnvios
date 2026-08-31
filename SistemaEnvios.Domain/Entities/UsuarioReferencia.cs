namespace SistemaEnvios.Domain.Entities;

public sealed class UsuarioReferencia
{
    public Guid UsuarioExternoId { get; set; }
    public string NombreCompleto { get; set; } = null!;
    public string? NumeroEmpleado { get; set; }
    public string? Correo { get; set; }
    public bool EsTecnico { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaUltimaSincronizacion { get; set; }
    public string Origen { get; set; } = "AUTHMANAGER";
    public byte[] RowVersion { get; set; } = [];
    public ICollection<Recepcion> RecepcionesAsignadas { get; set; } = [];
}
