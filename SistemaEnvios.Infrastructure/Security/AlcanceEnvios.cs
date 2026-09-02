using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Security;

public sealed class AlcanceEnvios(SistemaEnviosDbContext db, IUserContext usuario) : IAlcanceEnvios
{
    private static readonly string[] RolesGlobales =
        ["AdministradorGlobal", "SuperAdministrador", "AdministradorSistema"];

    /// <summary>
    /// El perfil sale del claim "position" contra PerfilesPorPosicion, no de la ubicación del
    /// usuario. En la sede conviven Tecnología, administradores de filial y asistentes
    /// administrativos con el mismo affiliate: deducirlo de la ubicación le daría alcance
    /// global a todo el centro.
    /// </summary>
    public async Task<PerfilAlcance> ResolverPerfilAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(usuario.Position))
        {
            var posicion = usuario.Position.Trim();
            var mapeado = await db.PerfilesPorPosicion.AsNoTracking()
                .Where(x => x.Posicion == posicion)
                .Select(x => (byte?)x.Perfil)
                .FirstOrDefaultAsync(cancellationToken);

            if (mapeado is byte perfil && Enum.IsDefined(typeof(PerfilAlcance), (int)perfil))
                return (PerfilAlcance)perfil;
        }

        if (TieneRolGlobal()) return PerfilAlcance.Global;

        // Posición sin mapear: se cae al alcance más restrictivo que el usuario pueda tener.
        return usuario.AffiliateId.HasValue ? PerfilAlcance.Filial : PerfilAlcance.SinAlcance;
    }

    public async Task<IQueryable<Envio>> FiltrarAsync(
        IQueryable<Envio> query,
        CancellationToken cancellationToken = default)
    {
        var perfil = await ResolverPerfilAsync(cancellationToken);
        var filialId = usuario.AffiliateId;

        return perfil switch
        {
            PerfilAlcance.Global => query,

            // Solo lo que Transportación custodia: sus etapas del flujo y transporte
            // institucional. El transporte privado va directo a Tecnología sin pasar por ellos.
            PerfilAlcance.Transportacion => query.Where(x =>
                EstadoEnvioCodigos.EtapasTransportacion.Contains(x.EstadoEnvio.Codigo) &&
                x.Transporte != null &&
                x.Transporte.TipoTransporte.Estrategia == EstrategiaTransporteEnum.TransportacionInstitucional),

            PerfilAlcance.Filial => query.Where(x =>
                x.UbicacionOrigen.FilialExternaId == filialId ||
                x.UbicacionDestino.FilialExternaId == filialId),

            _ => query.Where(_ => false)
        };
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
        if (perfil == PerfilAlcance.Global) return Result.Success();

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

    private bool TieneRolGlobal() =>
        usuario.Roles.Any(x => RolesGlobales.Contains(x, StringComparer.OrdinalIgnoreCase)) ||
        (!usuario.AffiliateId.HasValue &&
         usuario.Roles.Any(x => x.Equals("Administrador", StringComparison.OrdinalIgnoreCase)));
}
