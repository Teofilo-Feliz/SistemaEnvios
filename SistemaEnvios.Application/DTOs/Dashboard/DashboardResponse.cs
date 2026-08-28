namespace SistemaEnvios.Application.DTOs.Dashboard;

public sealed record DashboardResponse(
    DashboardSummary Summary,
    IReadOnlyCollection<DashboardPeriodPoint> Evolution,
    IReadOnlyCollection<DashboardStatusPoint> StatusByMonth,
    IReadOnlyCollection<DashboardLabelValue> ShipmentsByOrigin,
    IReadOnlyCollection<DashboardLabelValue> EquipmentByType);

public sealed record DashboardSummary(int TotalEnvios, int EnTransito, int Incidencias, int PendientesRecepcion, int Recibidos, int EnRevision, int Cerrados);
public sealed record DashboardPeriodPoint(string Month, int Total, int EnTransito, int Recibidos, int Cerrados);
public sealed record DashboardStatusPoint(string Month, string Estado, int Total);
public sealed record DashboardLabelValue(string Label, int Total);
