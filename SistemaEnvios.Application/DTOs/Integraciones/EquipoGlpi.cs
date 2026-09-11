namespace SistemaEnvios.Application.DTOs.Integraciones;

/// <summary>
/// Un activo de GLPI, ya leído de <c>{itemtype}/{id}?expand_dropdowns=true</c>.
/// </summary>
/// <remarks>
/// Todo es opcional porque en GLPI todo puede estar sin catalogar. Un equipo sin fabricante llega
/// con marca nula, y quien lo use tiene que dejar que la persona la escriba en vez de inventarla.
/// </remarks>
public sealed record EquipoGlpi(
    string ItemType,
    string? Marca,
    string? Modelo,
    string? Serial,
    string? CodigoActivo,
    string? Nombre,
    string? TipoGlpi,
    bool EnPapeleraOPlantilla);
