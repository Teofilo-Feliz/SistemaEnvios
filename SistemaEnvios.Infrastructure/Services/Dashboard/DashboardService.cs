using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Dashboard;
using SistemaEnvios.Application.Interfaces.Services.Dashboard;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Dashboard;

public sealed class DashboardService(SistemaEnviosDbContext db) : IDashboardService
{
    public async Task<Result<DashboardResponse>> ObtenerAsync(int meses = 12, CancellationToken cancellationToken = default)
    {
        meses = Math.Clamp(meses, 1, 24);
        var desde = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(meses - 1));
        var envios = db.Envios.AsNoTracking();
        var estados = await db.EstadosEnvio.AsNoTracking().ToDictionaryAsync(x => x.EstadoEnvioId, x => x.Codigo, cancellationToken);
        var resumen = new DashboardSummary(
            await envios.CountAsync(cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnTransito, cancellationToken),
            await db.Incidencias.CountAsync(cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.Codigo == EstadoEnvioCodigos.PendienteRecepcionFilial || x.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnEsperaDeTecnologia, cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.Codigo == EstadoEnvioCodigos.RecibidoPorTecnologia || x.EstadoEnvio.Codigo == EstadoEnvioCodigos.RecibidoEnFilial || x.EstadoEnvio.Codigo == EstadoEnvioCodigos.RecepcionValidadaEnFilial, cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnProcesoDeRevision, cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.EsFinal, cancellationToken));

        var recientes = await envios.Where(x => x.FechaCreacion >= desde).Select(x => new { x.FechaCreacion, x.EstadoEnvioId }).ToListAsync(cancellationToken);
        var finalStates = await db.EstadosEnvio.AsNoTracking().Where(x => x.EsFinal).Select(x => x.EstadoEnvioId).ToListAsync(cancellationToken);
        var evolution = Enumerable.Range(0, meses).Select(offset => desde.AddMonths(offset)).Select(month => new DashboardPeriodPoint(month.ToString("yyyy-MM"), recientes.Count(x => x.FechaCreacion.Year == month.Year && x.FechaCreacion.Month == month.Month), recientes.Count(x => x.FechaCreacion.Year == month.Year && x.FechaCreacion.Month == month.Month && estados[x.EstadoEnvioId] == EstadoEnvioCodigos.EnTransito), recientes.Count(x => x.FechaCreacion.Year == month.Year && x.FechaCreacion.Month == month.Month && (estados[x.EstadoEnvioId] == EstadoEnvioCodigos.RecibidoPorTecnologia || estados[x.EstadoEnvioId] == EstadoEnvioCodigos.RecibidoEnFilial)), recientes.Count(x => x.FechaCreacion.Year == month.Year && x.FechaCreacion.Month == month.Month && finalStates.Contains(x.EstadoEnvioId)))).ToArray();
        var statusByMonth = recientes.GroupBy(x => new { Month = x.FechaCreacion.ToString("yyyy-MM"), x.EstadoEnvioId }).Select(group => new DashboardStatusPoint(group.Key.Month, estados[group.Key.EstadoEnvioId], group.Count())).ToArray();
        var byOriginRows = await envios
            .GroupBy(x => x.UbicacionOrigen.Nombre)
            .Select(group => new { Label = group.Key, Total = group.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync(cancellationToken);
        var byOrigin = byOriginRows.Select(x => new DashboardLabelValue(x.Label, x.Total)).ToArray();

        var byTypeRows = await db.EnvioEquipos
            .Include(x => x.Equipo)
            .ThenInclude(x => x.TipoEquipo)
            .GroupBy(x => x.Equipo.TipoEquipo.Nombre)
            .Select(group => new { Label = group.Key, Total = group.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync(cancellationToken);
        var byType = byTypeRows.Select(x => new DashboardLabelValue(x.Label, x.Total)).ToArray();
        return Result<DashboardResponse>.Success(new DashboardResponse(resumen, evolution, statusByMonth, byOrigin, byType));
    }
}
