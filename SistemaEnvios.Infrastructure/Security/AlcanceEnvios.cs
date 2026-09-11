using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Security;

public sealed class AlcanceEnvios(
    SistemaEnviosDbContext db,
    IUserContext usuario,
    IMemoryCache cache,
    ILogger<AlcanceEnvios> registro) : IAlcanceEnvios
{
    /// <summary>
    /// El perfil sale de los ROLES del token, cruzados contra PerfilesPorPosicion. Ni de la
    /// posición ni de la ubicación del usuario: en la sede conviven Tecnología, administradores
    /// de filial y asistentes administrativos con el mismo affiliate, así que deducirlo de la
    /// ubicación le daría alcance global a todo el centro.
    /// </summary>
    /// <remarks>
    /// La posición dejó de conceder alcance. Es un cargo de recursos humanos: llega escrito de mil
    /// formas —con tilde y sin ella, en masculino y en femenino, con variantes que nadie
    /// registró— y, sobre todo, NO SE PUEDE REVOCAR desde donde se administran los accesos.
    ///
    /// Eso último es lo que decidió el cambio. Al sacar a alguien del grupo de super
    /// administradores conservaba el alcance, porque su cargo se lo seguía concediendo por una vía
    /// que ese grupo nunca controló. Quitar un acceso tiene que ser una sola acción y en un solo
    /// sitio; si no, se revoca creyendo que se revocó, que es peor que no revocar.
    ///
    /// Un rol, en cambio, es un grupo de seguridad que Tecnología concede y quita en AuthManager.
    /// Ahí vive el gobierno de los accesos, y ahora también su única llave.
    ///
    /// El alcance Global tiene además una segunda vía: el permiso "alcance.global". En AuthManager
    /// un rol se ata a un solo grupo de seguridad, así que darle vista global a un rol que ya existe
    /// obligaba a crear otro rol y sembrar su fila. Con el permiso se concede sin tocar la base.
    /// Suma al alcance del rol en vez de sustituirlo: gana el mayor de los dos.
    /// </remarks>
    public async Task<PerfilAlcance> ResolverPerfilAsync(CancellationToken cancellationToken = default)
    {
        var roles = usuario.Roles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        List<PerfilAlcance> candidatos = [];

        if (roles.Length > 0)
        {
            var porRol = await BuscarPerfilAsync(roles, cancellationToken);
            if (porRol is not null) candidatos.Add(porRol.Value);
        }

        if (usuario.Permissions.Contains(PermissionNames.AlcanceGlobal, StringComparer.OrdinalIgnoreCase))
            candidatos.Add(PerfilAlcance.Global);

        var elegido = candidatos.DeMayorAlcance();
        if (elegido is not null) return elegido.Value;

        var respaldo = usuario.AffiliateId.HasValue ? PerfilAlcance.Filial : PerfilAlcance.SinAlcance;
        AvisarRolSinMapear(roles, respaldo);
        return respaldo;
    }

    public async Task<IQueryable<Envio>> FiltrarAsync(
        IQueryable<Envio> query,
        CancellationToken cancellationToken = default)
    {
        var perfil = await ResolverPerfilAsync(cancellationToken);
        var filialId = usuario.AffiliateId;

        return perfil switch
        {
            PerfilAlcance.Global or PerfilAlcance.Tecnologia => query,

            PerfilAlcance.Transportacion => query.Where(x =>
                EstadoEnvioCodigos.EtapasTransportacion.Contains(x.EstadoEnvio.Codigo) &&
                (x.Transporte == null ||
                 x.Transporte.TipoTransporte.Estrategia == EstrategiaTransporteEnum.TransportacionInstitucional)),

            PerfilAlcance.Filial => query.Where(x =>
                x.UbicacionOrigen.FilialExternaId == filialId ||
                x.UbicacionDestino.FilialExternaId == filialId),

            _ => query.Where(_ => false)
        };
    }

    /// <summary>
    /// Un equipo pertenece a una ubicacion, no a un envio, asi que se acota por ahi.
    /// Transportacion ve los equipos que viajan en los envios bajo su custodia.
    /// </summary>
    public async Task<IQueryable<Equipo>> FiltrarEquiposAsync(
        IQueryable<Equipo> query,
        CancellationToken cancellationToken = default)
    {
        var perfil = await ResolverPerfilAsync(cancellationToken);
        var filialId = usuario.AffiliateId;

        return perfil switch
        {
            PerfilAlcance.Global or PerfilAlcance.Tecnologia => query,
            PerfilAlcance.Transportacion => query.Where(x => db.EnvioEquipos.Any(ee =>
                ee.EquipoId == x.EquipoId &&
                EstadoEnvioCodigos.EtapasTransportacion.Contains(ee.Envio.EstadoEnvio.Codigo))),
            PerfilAlcance.Filial => query.Where(x =>
                x.UbicacionActual.FilialExternaId == filialId ||
                db.EnvioEquipos.Any(ee => ee.EquipoId == x.EquipoId &&
                    (ee.Envio.UbicacionOrigen.FilialExternaId == filialId ||
                     ee.Envio.UbicacionDestino.FilialExternaId == filialId))),
            _ => query.Where(_ => false),
        };
    }

    public async Task<Result> VerificarUbicacionAsync(int ubicacionId, CancellationToken cancellationToken = default)
    {
        var perfil = await ResolverPerfilAsync(cancellationToken);
        if (perfil.EsTecnologia()) return Result.Success();

        var mapeada = await VerificarFilialMapeadaAsync(cancellationToken);
        if (mapeada.IsFailure) return mapeada;

        var filialId = usuario.AffiliateId;
        var alcanza = await db.Ubicaciones.AnyAsync(
            x => x.UbicacionId == ubicacionId && x.FilialExternaId == filialId, cancellationToken);

        return alcanza
            ? Result.Success()
            : Result.Failure("La ubicación no pertenece a su filial.", ErrorType.Forbidden);
    }

    public async Task<Result> VerificarFilialMapeadaAsync(CancellationToken cancellationToken = default)
    {
        var perfil = await ResolverPerfilAsync(cancellationToken);
        if (perfil == PerfilAlcance.SinAlcance)
            return Result.Failure("Su usuario no tiene una filial asignada.", ErrorType.Forbidden);
        if (perfil != PerfilAlcance.Filial)
            return Result.Success();

        var filialId = usuario.AffiliateId!.Value;
        var mapeada = await db.Ubicaciones.AnyAsync(x => x.FilialExternaId == filialId, cancellationToken);
        return mapeada
            ? Result.Success()
            : Result.Failure(
                $"La filial {filialId} de su usuario no está asociada a ninguna ubicación. Contacte a Tecnología.",
                ErrorType.Conflict);
    }

    public async Task<Result> VerificarAsync(int envioId, CancellationToken cancellationToken = default)
    {
        var perfil = await ResolverPerfilAsync(cancellationToken);
        if (perfil.EsTecnologia()) return Result.Success();

        var mapeada = await VerificarFilialMapeadaAsync(cancellationToken);
        if (mapeada.IsFailure) return mapeada;

        var query = await FiltrarAsync(db.Envios.AsNoTracking(), cancellationToken);
        var alcanza = await query.AnyAsync(x => x.EnvioId == envioId, cancellationToken);

        return alcanza
            ? Result.Success()
            : Result.Failure(
                perfil == PerfilAlcance.Transportacion
                    ? "El envío no está bajo custodia de Transportación."
                    : "El envío no pertenece a su filial.",
                ErrorType.Forbidden);
    }

    /// <summary>
    /// Un aviso por combinación de claves y por hora: ResolverPerfilAsync se llama varias veces
    /// por petición, y sin el tope el log se llenaría con la misma línea.
    /// </summary>
    private void AvisarRolSinMapear(string[] roles, PerfilAlcance respaldo)
    {
        var posicion = string.IsNullOrWhiteSpace(usuario.Position) ? "(sin posición)" : usuario.Position.Trim();
        var partesRoles = roles.Length == 0 ? "(sin roles)" : string.Join(", ", roles.Select(x => $"'{x}'"));
        var clave = $"aviso-perfil-sin-mapear::{posicion}|{partesRoles}";
        if (cache.TryGetValue(clave, out _)) return;
        cache.Set(clave, true, TimeSpan.FromHours(1));

        registro.LogWarning(
            "Perfil sin mapear: posición '{Posicion}'; roles {Roles}. Se aplicó el respaldo {Respaldo}, " +
            "así que este usuario aterriza en el módulo equivocado. El alcance solo lo concede un ROL: " +
            "inserte el nombre exacto de uno de estos roles en dbo.PerfilesPorPosicion (Perfil: 1=Global, 2=Transportacion, 3=Filial, 4=Tecnologia).",
            posicion, partesRoles, respaldo);
    }

    /// <summary>
    /// Busca el perfil de la primera clave mapeada. La tabla guarda posiciones y roles en la
    /// misma columna a propósito: para el sistema son lo mismo —un texto del token que decide
    /// el alcance— y separarlas obligaría a mantener dos catálogos con la misma regla.
    /// </summary>
    private async Task<PerfilAlcance?> BuscarPerfilAsync(string[] claves, CancellationToken cancellationToken)
    {
        var mapeados = await db.PerfilesPorPosicion.AsNoTracking()
            .Where(x => claves.Contains(x.Posicion))
            .Select(x => (int)x.Perfil)
            .ToArrayAsync(cancellationToken);

        return mapeados
            .Where(x => Enum.IsDefined(typeof(PerfilAlcance), x))
            .Select(x => (PerfilAlcance)x)
            .DeMayorAlcance();
    }
}
