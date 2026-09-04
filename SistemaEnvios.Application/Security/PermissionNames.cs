namespace SistemaEnvios.Application.Security;

public static class PermissionNames
{
    public const string EnviosConsultar = "envios.consultar";
    public const string EnviosCrear = "envios.crear";
    public const string EnviosEditar = "envios.editar";
    public const string EnviosDespachar = "envios.despachar";
    public const string TransportesGestionar = "transportes.gestionar";
    public const string TransportesConfirmar = "transportes.confirmar";
    // Mantener la flota (tipos de transporte y choferes internos) es distinto de asignarle
    // transporte a un envío: eso último lo hace cada filial con "transportes.gestionar".
    public const string TransportesAdministrar = "transportes.administrar";
    public const string RecepcionesGestionar = "recepciones.gestionar";
    public const string IncidenciasGestionar = "incidencias.gestionar";
    public const string EquiposGestionar = "equipos.gestionar";
    public const string CatalogosAdministrar = "catalogos.administrar";
}
