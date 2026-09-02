namespace SistemaEnvios.Domain.Constants;

public static class EstadoEnvioCodigos
{
    public const string EnFilial = "EN_FILIAL";
    public const string EntregadoATransportacion = "ENTREGADO_TRANSPORTACION";
    public const string DespachadoTransportePrivado = "DESPACHADO_TRANSPORTE_PRIVADO";
    public const string EnProcesoConfirmacionTransportacion = "PENDIENTE_CONFIRMACION_TRANSPORTE";
    public const string ConfirmadoPorTransportacion = "CONFIRMADO_TRANSPORTACION";
    public const string EnTransito = "EN_TRANSITO";
    public const string RecibidoPorTransportacion = "RECIBIDO_TRANSPORTACION";
    public const string EnEsperaDeTecnologia = "ESPERA_TECNOLOGIA";
    public const string EnProcesoDeRevision = "EN_REVISION";
    public const string RecibidoPorTecnologia = "RECIBIDO_TECNOLOGIA";
    public const string IncidenciaEnTransportacion = "INCIDENCIA_TRANSPORTACION";

    public const string EnPreparacionTecnologia = "PREPARACION_TECNOLOGIA";
    public const string DespachadoPorTecnologia = "DESPACHADO_TECNOLOGIA";
    public const string EnTransportacion = "EN_TRANSPORTACION";
    public const string TransporteAsignado = "TRANSPORTE_ASIGNADO";
    public const string DespachadoPorTransportacion = "DESPACHADO_TRANSPORTACION";
    public const string PendienteRecepcionFilial = "PENDIENTE_RECEPCION_FILIAL";
    public const string RecibidoEnFilial = "RECIBIDO_FILIAL";
    public const string RecepcionValidadaEnFilial = "RECEPCION_VALIDADA_FILIAL";

    /// <summary>
    /// Etapas en las que el envío está bajo custodia de Transportación, en cualquiera de las
    /// dos direcciones del flujo. Fuera de estas el envío es de la filial o de Tecnología.
    /// </summary>
    public static readonly string[] EtapasTransportacion =
    [
        EntregadoATransportacion,
        EnTransito,
        RecibidoPorTransportacion,
        IncidenciaEnTransportacion,
        DespachadoPorTecnologia,
        EnTransportacion,
        TransporteAsignado,
        DespachadoPorTransportacion
    ];
}
