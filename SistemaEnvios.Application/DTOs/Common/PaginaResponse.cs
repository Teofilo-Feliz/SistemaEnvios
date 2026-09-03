namespace SistemaEnvios.Application.DTOs.Common;

/// <summary>
/// Una página de resultados. Lleva el total para que el cliente pueda dibujar el paginador sin
/// una segunda consulta, y conserva los nombres del contrato que ya consume el frontend.
/// </summary>
public sealed record PaginaResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages)
{
    public static PaginaResponse<T> Crear(IReadOnlyCollection<T> items, ParametrosPagina parametros, int totalItems) =>
        new(items,
            parametros.PaginaNormalizada,
            parametros.TamanoNormalizado,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)parametros.TamanoNormalizado));
}
