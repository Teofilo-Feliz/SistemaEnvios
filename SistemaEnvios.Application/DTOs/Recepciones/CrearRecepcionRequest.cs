namespace SistemaEnvios.Application.DTOs.Recepciones;

public sealed class CrearRecepcionRequest
{
    public int EnvioId { get; init; }
    public int? TecnicoAsignadoId { get; init; }
    public string? Observaciones { get; init; }
}
