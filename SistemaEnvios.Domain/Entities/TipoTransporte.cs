using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Domain.Entities;

public sealed class TipoTransporte : AuditoriaEntitie
{
    public int TipoTransporteId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public EstrategiaTransporteEnum Estrategia { get; set; }
    public bool Activo { get; set; }
    public ICollection<Transporte> Transportes { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
