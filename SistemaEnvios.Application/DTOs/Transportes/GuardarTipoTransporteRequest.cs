using SistemaEnvios.Domain.Enums;
namespace SistemaEnvios.Application.DTOs.Transportes;
public sealed class GuardarTipoTransporteRequest { public int TipoTransporteId { get; init; } public string Codigo { get; init; } = null!; public string Nombre { get; init; } = null!; public EstrategiaTransporteEnum Estrategia { get; init; } }
