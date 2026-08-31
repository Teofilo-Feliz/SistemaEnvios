namespace SistemaEnvios.Application.DTOs.Recepciones;

public sealed class AsignarTecnicoRequest
{
    public int RecepcionId { get; init; }
    public Guid TecnicoAsignadoUsuarioId { get; init; }
}
