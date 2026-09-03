using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Infrastructure.Persistence;

public static class ConsultasPaginadas
{
    /// <summary>
    /// Corta la consulta en la base de datos, no en memoria: el Skip/Take viaja como OFFSET/FETCH
    /// y solo la página pedida cruza la red. El total se cuenta aparte para que el cliente pueda
    /// dibujar el paginador sin una segunda llamada.
    ///
    /// La consulta debe llegar ordenada; sin ORDER BY, SQL Server rechaza el OFFSET y además el
    /// orden de las páginas quedaría a criterio del motor.
    /// </summary>
    public static async Task<PaginaResponse<T>> PaginarAsync<TEntidad, T>(
        this IQueryable<TEntidad> query,
        ParametrosPagina parametros,
        Expression<Func<TEntidad, T>> proyeccion,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(parametros.Salto)
            .Take(parametros.TamanoNormalizado)
            .Select(proyeccion)
            .ToListAsync(cancellationToken);

        return PaginaResponse<T>.Crear(items, parametros, total);
    }
}
