using SistemaEnvios.Application.Common;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Interfaces.Security;

/// <summary>Qué subconjunto de envíos le corresponde a un usuario.</summary>
public enum PerfilAlcance
{
    /// <summary>Autenticado pero sin filial ni permisos que le den alcance: no ve nada.</summary>
    SinAlcance = 0,
    /// <summary>Rol administrador global, o usuario cuya ubicación es Tecnología.</summary>
    Global = 1,
    /// <summary>Ve las etapas que custodia, en todas las filiales, solo del transporte institucional.</summary>
    Transportacion = 2,
    /// <summary>Ve los envíos cuyo origen o destino es su filial.</summary>
    Filial = 3,
    /// <summary>
    /// Soporte técnico. Mismo alcance de datos que <see cref="Global"/> —Tecnología está en un
    /// extremo de todo envío y es la única que descarta equipos— pero trabaja dentro de su
    /// módulo: no administra catálogos ni la flota. Esa parte la deciden sus permisos y el mapa
    /// de módulos del frontend, no este enum.
    /// </summary>
    Tecnologia = 4
}

public static class PerfilAlcanceExtensiones
{
    /// <summary>
    /// Los perfiles que mandan sobre todo el sistema.
    /// </summary>
    /// <remarks>
    /// Esta comparación estaba repetida en ocho sitios como <c>== PerfilAlcance.Global</c>, y
    /// varios de esos sitios ya decían "Solo Tecnología puede…" en su mensaje de error: el
    /// perfil se llamaba Global pero significaba Tecnología. Al separar soporte técnico en su
    /// propio perfil, ocho comparaciones sueltas eran ocho oportunidades de olvidar una en
    /// código que decide quién ve qué. Aquí la regla vive una sola vez.
    /// </remarks>
    public static bool EsTecnologia(this PerfilAlcance perfil) =>
        perfil is PerfilAlcance.Global or PerfilAlcance.Tecnologia;

    /// <summary>
    /// Orden de alcance, de mayor a menor. Cuando un usuario trae varias claves mapeadas, gana la
    /// primera de esta lista.
    /// </summary>
    /// <remarks>
    /// Un usuario puede traer varios roles a la vez. Queren, por ejemplo, llega con "Soporte
    /// Técnico" y "SuperAdministrador": sin un orden explícito, la consulta se quedaba con el
    /// primero que devolviera SQL Server, y eso no está definido —depende del plan, del índice y
    /// del orden físico de las filas—. El mismo usuario podía resolver a un perfil distinto entre
    /// despliegues sin que nadie tocara nada, que es peor que fallar siempre.
    ///
    /// El criterio es que conceder un rol nunca quite lo que ya se tenía: si a alguien le dan el
    /// grupo de super administrador, espera poder más, no menos.
    ///
    /// Tecnología va por delante de Transportación y Filial aunque su número sea mayor: ve los
    /// mismos datos que Global —está en un extremo de todo envío— y lo que la acota son las
    /// pantallas, no el alcance. Por eso el orden no es el numérico del enum.
    /// </remarks>
    private static readonly PerfilAlcance[] DeMayorAMenorAlcance =
    [
        PerfilAlcance.Global,
        PerfilAlcance.Tecnologia,
        PerfilAlcance.Transportacion,
        PerfilAlcance.Filial
    ];

    /// <summary>Posición en el orden de alcance; los desconocidos van al final.</summary>
    public static int Precedencia(this PerfilAlcance perfil)
    {
        var indice = Array.IndexOf(DeMayorAMenorAlcance, perfil);
        return indice < 0 ? int.MaxValue : indice;
    }

    /// <summary>El de mayor alcance de los que se le reconocen al usuario, o null si ninguno.</summary>
    public static PerfilAlcance? DeMayorAlcance(this IEnumerable<PerfilAlcance> perfiles)
    {
        PerfilAlcance? mejor = null;
        foreach (var perfil in perfiles)
            if (mejor is null || perfil.Precedencia() < mejor.Value.Precedencia())
                mejor = perfil;
        return mejor;
    }
}

/// <summary>
/// Restringe los envíos al alcance del usuario autenticado. Un permiso dice QUÉ puede hacer;
/// esto dice SOBRE CUÁLES. El claim "affiliate" de AuthManager llega como
/// "30,SANTO DOMINGO (SEDE)" y se cruza con Ubicacion.FilialExternaId.
/// </summary>
public interface IAlcanceEnvios
{
    /// <summary>Perfil efectivo del usuario. Requiere consulta porque Tecnología se resuelve por datos.</summary>
    Task<PerfilAlcance> ResolverPerfilAsync(CancellationToken cancellationToken = default);

    /// <summary>Restringe una consulta de envíos al alcance del usuario.</summary>
    Task<IQueryable<Envio>> FiltrarAsync(IQueryable<Envio> query, CancellationToken cancellationToken = default);

    /// <summary>Verifica que el usuario pueda operar sobre un envío concreto.</summary>
    Task<Result> VerificarAsync(int envioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ubicaciones sobre las que el usuario puede operar. Un perfil de filial alcanza solo la
    /// suya; el inventario de equipos se acota por aquí, ya que un equipo pertenece a un lugar
    /// y no a un envío.
    /// </summary>
    Task<Result> VerificarUbicacionAsync(int ubicacionId, CancellationToken cancellationToken = default);

    /// <summary>Restringe una consulta de equipos a las ubicaciones que el usuario alcanza.</summary>
    Task<IQueryable<Equipo>> FiltrarEquiposAsync(IQueryable<Equipo> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Falla con Conflict si el usuario es de filial y su filial no está mapeada a ninguna
    /// ubicación, para que el error de configuración no se confunda con "no hay datos".
    /// </summary>
    Task<Result> VerificarFilialMapeadaAsync(CancellationToken cancellationToken = default);
}
