namespace SistemaEnvios.Application.DTOs.Dashboard;

/// <summary>
/// Totales de la operación de Transportación, contados en la base. La pantalla los dibuja tal
/// cual: no recibe envíos para contarlos por su cuenta.
/// </summary>
public sealed record DashboardTransportacionResponse(
    IReadOnlyCollection<DashboardEtapaPoint> PorEtapa,
    IReadOnlyCollection<DashboardLabelValue> PorTipoTransporte,
    int TotalEnEtapas);

/// <summary>
/// El total se abre por dirección porque "en tránsito" son dos tarjetas distintas en la
/// pantalla: hacia Tecnología y hacia la filial.
/// </summary>
public sealed record DashboardEtapaPoint(string Codigo, string Nombre, int Direccion, int Total);
