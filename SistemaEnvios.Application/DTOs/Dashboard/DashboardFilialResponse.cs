namespace SistemaEnvios.Application.DTOs.Dashboard;

/// <summary>
/// Tablero de una filial, contado en la base y acotado a lo suyo. Devuelve números por etapa en
/// vez de tarjetas ya rotuladas: cómo se agrupan en pantalla es decisión de la vista, y así un
/// cambio de rótulo no obliga a tocar el API.
/// </summary>
/// <param name="EquiposFuera">
/// Equipos de la filial con un caso abierto: salieron y todavía no han vuelto. Es el número que
/// dice si algo se quedó atascado en Tecnología.
/// </param>
public sealed record DashboardFilialResponse(
    string FilialNombre,
    IReadOnlyCollection<DashboardEtapaPoint> PorEtapa,
    int EquiposEnFilial,
    int EquiposFuera,
    int CasoMasAntiguoEnDias);
