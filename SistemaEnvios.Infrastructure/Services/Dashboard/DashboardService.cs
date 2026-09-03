using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Dashboard;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Interfaces.Services.Dashboard;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Services.Dashboard;

public sealed class DashboardService(
    SistemaEnviosDbContext db,
    IAlcanceEnvios alcance,
    IUserContext usuario) : IDashboardService
{
    /// <summary>
    /// Todo sale del filtro de alcance, así que una filial cuenta solo lo suyo sin que este
    /// método sepa nada de filiales: la regla vive en un único sitio.
    /// </summary>
    public async Task<Result<DashboardFilialResponse>> ObtenerFilialAsync(CancellationToken cancellationToken = default)
    {
        if (await alcance.ResolverPerfilAsync(cancellationToken) != PerfilAlcance.Filial)
            return Result<DashboardFilialResponse>.Failure(
                "Este tablero es de las filiales; su usuario no pertenece a una.", ErrorType.Forbidden);

        var mapeada = await alcance.VerificarFilialMapeadaAsync(cancellationToken);
        if (mapeada.IsFailure)
            return Result<DashboardFilialResponse>.Failure(mapeada.Error!, mapeada.ErrorType);

        var filialId = usuario.AffiliateId!.Value;
        var filial = await db.Ubicaciones.AsNoTracking()
            .Where(x => x.FilialExternaId == filialId)
            .Select(x => new { x.UbicacionId, x.Nombre })
            .FirstAsync(cancellationToken);

        var envios = await alcance.FiltrarAsync(db.Envios.AsNoTracking(), cancellationToken);
        var porEtapa = await envios
            .GroupBy(x => new { x.EstadoEnvio.Codigo, x.EstadoEnvio.Nombre, x.Direccion })
            .Select(g => new DashboardEtapaPoint(g.Key.Codigo, g.Key.Nombre, (int)g.Key.Direccion, g.Count()))
            .ToListAsync(cancellationToken);

        var equiposEnFilial = await db.Equipos.AsNoTracking()
            .CountAsync(x => x.UbicacionActualId == filial.UbicacionId, cancellationToken);

        // Casos abiertos de la filial: equipos que salieron y todavía no han vuelto.
        var abiertos = db.EnvioEquipos.AsNoTracking().Where(x =>
            x.EnvioEquipoOrigenId == null &&
            x.FechaCierreCaso == null &&
            (x.Envio.Direccion == DireccionEnvioEnum.HaciaTecnologia
                ? x.Envio.UbicacionOrigenId
                : x.Envio.UbicacionDestinoId) == filial.UbicacionId);

        var equiposFuera = await abiertos.CountAsync(cancellationToken);
        var masAntiguo = await abiertos
            .OrderBy(x => x.FechaCreacion)
            .Select(x => (DateTime?)x.FechaCreacion)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<DashboardFilialResponse>.Success(new(
            filial.Nombre,
            [.. porEtapa.OrderByDescending(x => x.Total)],
            equiposEnFilial,
            equiposFuera,
            masAntiguo is null ? 0 : (int)(DateTime.UtcNow - masAntiguo.Value).TotalDays));
    }

    public async Task<Result<DashboardTransportacionResponse>> ObtenerTransportacionAsync(CancellationToken cancellationToken = default)
    {
        var envios = await alcance.FiltrarAsync(db.Envios.AsNoTracking(), cancellationToken);
        var enEtapas = envios.Where(x => EstadoEnvioCodigos.EtapasTransportacion.Contains(x.EstadoEnvio.Codigo));

        var porEtapa = await enEtapas
            .GroupBy(x => new { x.EstadoEnvio.Codigo, x.EstadoEnvio.Nombre, x.Direccion })
            .Select(g => new DashboardEtapaPoint(g.Key.Codigo, g.Key.Nombre, (int)g.Key.Direccion, g.Count()))
            .ToListAsync(cancellationToken);

        var porTipo = await enEtapas
            .GroupBy(x => x.Transporte == null ? "Sin transporte" : x.Transporte.TipoTransporte.Nombre)
            .Select(g => new DashboardLabelValue(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        return Result<DashboardTransportacionResponse>.Success(new(
            [.. porEtapa.OrderByDescending(x => x.Total)],
            [.. porTipo.OrderByDescending(x => x.Total)],
            porEtapa.Sum(x => x.Total)));
    }

    public async Task<Result<DashboardResponse>> ObtenerAsync(int meses = 12, CancellationToken cancellationToken = default)
    {
        meses = Math.Clamp(meses, 1, 24);
        var desde = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(meses - 1));
        var envios = await alcance.FiltrarAsync(db.Envios.AsNoTracking(), cancellationToken);
        var enAlcance = envios.Select(x => x.EnvioId);
        var estados = await db.EstadosEnvio.AsNoTracking().ToDictionaryAsync(x => x.EstadoEnvioId, x => x.Codigo, cancellationToken);
        var resumen = new DashboardSummary(
            await envios.CountAsync(cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnTransito, cancellationToken),
            await db.Incidencias.CountAsync(x => enAlcance.Contains(x.EnvioId), cancellationToken),
            await envios.CountAsync(x => x.EstadoEnvio.Codigo == EstadoEnvioCodigos.PendienteRecepcionFilial || x.EstadoEnvio.Codigo == EstadoEnvioCodigos.EnEsperaDeTecnologia || x.EstadoEnvio.Codigo == EstadoEnvioCodigos.RecibidoPorTransportacion, cancellationToken),
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
            .Where(x => enAlcance.Contains(x.EnvioId))
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
