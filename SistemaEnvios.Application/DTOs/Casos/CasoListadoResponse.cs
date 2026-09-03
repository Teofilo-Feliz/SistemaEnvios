using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Application.DTOs.Casos;

/// <summary>
/// Fila de la pantalla de descarte: un equipo que está en Tecnología con su caso todavía
/// abierto. Trae la filial dueña y las vueltas que lleva, que es lo que permite decidir si el
/// equipo vuelve o se descarta.
/// </summary>
public sealed record CasoListadoResponse(
    int EquipoId,
    string? NumeroSerie,
    string? CodigoActivo,
    string Marca,
    string Modelo,
    string NumeroTicket,
    int FilialId,
    string FilialNombre,
    int Movimientos,
    DateTime FechaApertura,
    int DiasAbierto);

public sealed class ConsultarCasosRequest : ParametrosPagina
{
    /// <summary>Busca por serial, código de activo o número de ticket.</summary>
    public string? Search { get; init; }
    public int? FilialId { get; init; }
}

public sealed class DescartarEquipoRequest
{
    public int EquipoId { get; init; }
    public string Motivo { get; init; } = string.Empty;
}
