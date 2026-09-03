namespace SistemaEnvios.Application.DTOs.Common;

/// <summary>
/// Paginación sin filtros propios, para los listados que ya vienen acotados por su padre
/// (el historial de un envío, sus equipos, sus incidencias). Acotado no es lo mismo que corto:
/// un envío con muchos movimientos igual conviene traerlo por partes.
/// </summary>
public sealed class ParametrosPaginaSimple : ParametrosPagina;
