using SistemaEnvios.Domain.Enums;
namespace SistemaEnvios.Application.DTOs.Transportes;
public sealed record TipoTransporteResponse(int TipoTransporteId, string Codigo, string Nombre, EstrategiaTransporteEnum Estrategia, bool Activo);
