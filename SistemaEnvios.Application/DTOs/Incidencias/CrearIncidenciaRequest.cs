namespace SistemaEnvios.Application.DTOs.Incidencias;

public sealed class CrearIncidenciaRequest
{
    public int EnvioId { get; init; }
    public int? EnvioEquipoId { get; init; }
    public int? TransporteId { get; init; }
    public string Descripcion { get; init; } = null!;
    public Guid UsuarioId { get; init; }
}
