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
    /// El perfil sale del claim "position" contra PerfilesPorPosicion, no de la ubicación del
    /// usuario. En la sede conviven Tecnología, administradores de filial y asistentes
    /// administrativos con el mismo affiliate: deducirlo de la ubicación le daría alcance
    /// global a todo el centro.
    /// </summary>
    public async Task<PerfilAlcance> ResolverPerfilAsync(CancellationToken cancellationToken = default)
    {
        // La posición manda: es del cargo concreto, mientras que un rol agrupa a mucha gente.
        if (!string.IsNullOrWhiteSpace(usuario.Position))
        {
            var perfilDePosicion = await BuscarPerfilAsync([usuario.Position.Trim()], cancellationToken);
            if (perfilDePosicion is not null) return perfilDePosicion.Value;
        }

        // Un rol creado a propósito para este sistema es más fiable de administrar que la
        // posición, que es un cargo de recursos humanos y puede venir escrito de mil formas.
        var roles = usuario.Roles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        if (roles.Length > 0)
        {
            var perfilDeRol = await BuscarPerfilAsync(roles, cancellationToken);
            if (perfilDeRol is not null) return perfilDeRol.Value;
        }

        // Aquí había un respaldo que concedía alcance Global por el nombre del rol
        // ("AdministradorGlobal" y parecidos). Esos nombres los define AuthManager para todas
        // sus aplicaciones, así que un administrador de otra entraba aquí viéndolo todo. El
        // alcance Global se concede mapeando la posición o el rol, nunca por cómo se llame.

        // Posición sin mapear: se cae al alcance más restrictivo que el usuario pueda tener.
        // El respaldo es silencioso por diseño —nadie se queda fuera del sistema— pero eso
        // esconde el síntoma real: Transportación aterriza en el tablero de filial y parece un
        // fallo de enrutamiento, no una fila que falta. Se registra la clave exacta que trajo el
        // token para saber qué insertar, igual que hace PermisosPorPosicionTransformation.
        var respaldo = usuario.AffiliateId.HasValue ? PerfilAlcance.Filial : PerfilAlcance.SinAlcance;
        AvisarPosicionSinMapear(roles, respaldo);
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
            // Tecnología está en un extremo de todo envío, así que los ve todos. Lo que la
            // separa de Global no son los datos sino las pantallas que puede abrir.
            PerfilAlcance.Global or PerfilAlcance.Tecnologia => query,

            // Solo lo que Transportación custodia: sus etapas del flujo y transporte
            // institucional. El transporte privado va directo a Tecnología sin pasar por ellos.
            // Sin transporte todavía es el caso normal, no una excepción: Tecnología despacha y
            // el envío queda esperando a que Transportación le asigne chofer, que es cuando nace
            // el transporte. Exigirlo aquí escondía justo los envíos que ella tiene que atender.
            //
            // El transporte privado se sigue excluyendo: su flujo no deja mover un envío sin un
            // transporte válido, así que "sin transporte" nunca significa privado.
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
            // Los suyos, más los que viajan en un envío suyo. Sin esta segunda parte la filial
            // no podía ver lo que le venía en camino, que es justo lo que tiene que mirar para
            // recibirlo: el equipo sigue en Tecnología hasta que ella lo recibe.
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
    private void AvisarPosicionSinMapear(string[] roles, PerfilAlcance respaldo)
    {
        var posicion = string.IsNullOrWhiteSpace(usuario.Position) ? "(sin posición)" : usuario.Position.Trim();
        var partesRoles = roles.Length == 0 ? "(sin roles)" : string.Join(", ", roles.Select(x => $"'{x}'"));
        var clave = $"aviso-perfil-sin-mapear::{posicion}|{partesRoles}";
        if (cache.TryGetValue(clave, out _)) return;
        cache.Set(clave, true, TimeSpan.FromHours(1));

        registro.LogWarning(
            "Perfil sin mapear: posición '{Posicion}'; roles {Roles}. Se aplicó el respaldo {Respaldo}, " +
            "así que este usuario aterriza en el módulo equivocado. Inserte la clave exacta en " +
            "dbo.PerfilesPorPosicion (Perfil: 1=Global, 2=Transportacion, 3=Filial, 4=Tecnologia).",
            posicion, partesRoles, respaldo);
    }

    /// <summary>
    /// Busca el perfil de la primera clave mapeada. La tabla guarda posiciones y roles en la
    /// misma columna a propósito: para el sistema son lo mismo —un texto del token que decide
    /// el alcance— y separarlas obligaría a mantener dos catálogos con la misma regla.
    /// </summary>
    private async Task<PerfilAlcance?> BuscarPerfilAsync(string[] claves, CancellationToken cancellationToken)
    {
        var mapeado = await db.PerfilesPorPosicion.AsNoTracking()
            .Where(x => claves.Contains(x.Posicion))
            .Select(x => (byte?)x.Perfil)
            .FirstOrDefaultAsync(cancellationToken);

        return mapeado is byte perfil && Enum.IsDefined(typeof(PerfilAlcance), (int)perfil)
            ? (PerfilAlcance)perfil
            : null;
    }

}
