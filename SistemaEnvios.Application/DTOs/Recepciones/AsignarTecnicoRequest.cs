namespace SistemaEnvios.Application.DTOs.Recepciones;

public sealed class AsignarTecnicoRequest
{
    public int RecepcionId { get; init; }
    public int TecnicoAsignadoId { get; init; }
}
