using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Api.Infrastructure;

/// <summary>
/// Compara el reloj del API con el de SQL Server al arrancar.
/// </summary>
/// <remarks>
/// Desde que NumeroEnvio se calcula a partir de FechaCreacion, quien decide el año y el mes de
/// cada envío es el reloj del servidor de base de datos. Y ese es justo el que nadie vigila: el
/// del API lo vigila la autenticación —un token con más de unos minutos de desfase se rechaza y
/// el sistema deja de funcionar entero—, pero el de SQL Server no tiene quien lo mire.
///
/// Esto le pone testigo. No bloquea el arranque a propósito: un API que se niega a levantar
/// porque el reloj está corrido un minuto es peor que el problema que evita.
/// </remarks>
public static class RelojDeLaBase
{
    /// <summary>
    /// A partir de aquí se avisa. Un minuto tolera el desfase normal entre dos máquinas
    /// sincronizadas y sigue siendo mucho menos que lo que haría falta para que un envío se
    /// numere con el mes equivocado.
    /// </summary>
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(1);

    public static async Task ComprobarAsync(IServiceProvider servicios, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await using var ambito = servicios.CreateAsyncScope();
            var db = ambito.ServiceProvider.GetRequiredService<SistemaEnviosDbContext>();

            var enLaBase = await db.Database
                .SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value")
                .SingleAsync(ct);

            var enElApi = DateTime.UtcNow;
            var desfase = enLaBase - enElApi;

            if (desfase.Duration() <= Tolerancia)
            {
                logger.LogInformation(
                    "Relojes en hora: la base y el API difieren en {Segundos:0.0} s.",
                    desfase.TotalSeconds);
                return;
            }

            logger.LogWarning(
                "El reloj de la base y el del API difieren en {Segundos:0.0} s (base {EnLaBase:O}, API {EnElApi:O}). " +
                "La fecha de cada envío la sella la base, y de ella salen el año y el mes de su número. " +
                "Sincronice por NTP el servidor de SQL Server.",
                desfase.TotalSeconds, enLaBase, enElApi);
        }
        catch (Exception ex)
        {
            // Sin base no hay nada que comparar, y quedarse sin arrancar por eso sería sustituir
            // un aviso por una caída. El chequeo de salud ya cubre que la base no responda.
            logger.LogWarning(ex, "No se pudo comparar el reloj con el de la base de datos.");
        }
    }
}
