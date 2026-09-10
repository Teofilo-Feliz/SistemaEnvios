using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Domain.Enums;
namespace SistemaEnvios.Application.DTOs.Envios;
public sealed class ConsultarEnviosRequest : ParametrosPagina
{
    public string? Search { get; init; }
    public int? EstadoEnvioId { get; init; }
    public int? TipoTransporteId { get; init; }
    public int? UbicacionOrigenId { get; init; }
    public int? UbicacionDestinoId { get; init; }
    /// <summary>Envíos que tocan esta ubicación en cualquiera de los dos extremos.</summary>
    public int? UbicacionId { get; init; }
    public int? Direccion { get; init; }

    /// <summary>
    /// Etapas concretas que interesan, por código. Cada pantalla operativa mira un puñado
    /// distinto; sin esto tenían que pedir todos los envíos y descartar en el navegador.
    /// </summary>
    public string[]? EstadoCodigos { get; init; }

    /// <summary>
    /// Solo los envíos de esta estrategia de transporte. Lo usa el módulo de Tecnología para
    /// listar los privados, que llegan sin pasar por Transportación.
    /// </summary>
    public EstrategiaTransporteEnum? EstrategiaTransporte { get; init; }

    /// <summary>
    /// Todo menos esta estrategia, incluidos los envíos que todavía no tienen transporte.
    /// </summary>
    /// <remarks>
    /// Es lo que necesita el módulo de Transportación para excluir el privado, y no puede
    /// escribirse como "solo institucional": un envío que Tecnología acaba de despachar aún no
    /// tiene transporte —nace cuando Transportación le asigna chofer— y es justo el que ella
    /// tiene que atender. Misma forma que el predicado de AlcanceEnvios para ese perfil.
    /// </remarks>
    public EstrategiaTransporteEnum? ExcluirEstrategiaTransporte { get; init; }
}
