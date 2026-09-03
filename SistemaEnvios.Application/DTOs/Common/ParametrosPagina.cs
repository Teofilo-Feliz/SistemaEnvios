namespace SistemaEnvios.Application.DTOs.Common;

/// <summary>
/// Base de todo filtro que devuelve una lista. Normaliza aquí y no en cada servicio: el tope de
/// tamaño es lo único que impide que un cliente pida la tabla entera en una sola petición, y
/// repetir esa regla en ocho listados es repetir ocho oportunidades de olvidarla.
/// </summary>
public abstract class ParametrosPagina
{
    public const int TamanoMaximo = 100;
    public const int TamanoPorDefecto = 20;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = TamanoPorDefecto;

    public int PaginaNormalizada => Page < 1 ? 1 : Page;

    /// <summary>
    /// Un tamaño inválido cae al valor por defecto y no a cero: pedir mal la página no debería
    /// devolver una lista vacía que parezca "no hay datos".
    /// </summary>
    public int TamanoNormalizado => PageSize < 1 ? TamanoPorDefecto : Math.Min(PageSize, TamanoMaximo);

    public int Salto => (PaginaNormalizada - 1) * TamanoNormalizado;
}
