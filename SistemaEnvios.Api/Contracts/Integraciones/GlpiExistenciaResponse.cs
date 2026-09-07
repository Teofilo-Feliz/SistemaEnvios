namespace SistemaEnvios.Api.Contracts.Integraciones;

/// <summary>
/// Respuesta de una consulta de existencia contra GLPI. Devuelve el tipo y el id consultados
/// además del booleano para que un cliente que dispara varias consultas pueda emparejar la
/// respuesta con su pregunta sin depender del orden.
/// </summary>
public sealed record GlpiExistenciaResponse(string ItemType, int Id, bool Existe);
