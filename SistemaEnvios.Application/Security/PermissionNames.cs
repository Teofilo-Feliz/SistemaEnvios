namespace SistemaEnvios.Application.Security;

public static class PermissionNames
{
    public const string EnviosConsultar = "envios.consultar";
    public const string EnviosCrear = "envios.crear";
    public const string EnviosEditar = "envios.editar";
    public const string EnviosDespachar = "envios.despachar";
    public const string TransportesGestionar = "transportes.gestionar";
    public const string TransportesConfirmar = "transportes.confirmar";

    /// <summary>
    /// Mantener la flota (tipos de transporte y choferes internos) es distinto de asignarle
    /// transporte a un envío: eso último lo hace cada filial con <see cref="TransportesGestionar"/>.
    /// </summary>
    public const string TransportesAdministrar = "transportes.administrar";

    public const string RecepcionesGestionar = "recepciones.gestionar";
    public const string IncidenciasGestionar = "incidencias.gestionar";
    public const string EquiposGestionar = "equipos.gestionar";
    public const string CatalogosAdministrar = "catalogos.administrar";

    /// <summary>
    /// Concede alcance Global: ver los envíos de las 34 filiales, no solo los de la propia.
    /// </summary>
    /// <remarks>
    /// Es el único permiso que no autoriza una acción sino que amplía el alcance, y por eso no
    /// lleva el prefijo de un recurso. Los demás dicen QUÉ puede hacer el usuario; este dice
    /// SOBRE CUÁLES envíos.
    ///
    /// Existe porque en AuthManager un rol se ata a un solo grupo de seguridad, así que dar
    /// alcance global exigía crear un rol dedicado y sembrar su fila en PerfilesPorPosicion. Con
    /// este permiso se concede desde AuthManager a cualquier rol que ya exista, sin tocar la base.
    ///
    /// Suma, no sustituye: si el usuario ya tenía alcance por su rol, gana el mayor de los dos.
    /// Es la misma regla que rige entre roles —conceder algo nunca quita lo que otro ya daba— y
    /// no una excepción aparte que haya que recordar.
    ///
    /// No tiene política en AuthorizationConfiguration a propósito: ningún endpoint lo exige.
    /// Lo lee AlcanceEnvios.ResolverPerfilAsync, que es quien decide el alcance.
    /// </remarks>
    public const string AlcanceGlobal = "alcance.global";
}
