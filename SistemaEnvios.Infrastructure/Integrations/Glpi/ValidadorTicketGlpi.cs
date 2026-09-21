using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <inheritdoc cref="IValidadorTicketGlpi"/>
public sealed class ValidadorTicketGlpi(
    IGlpiClient glpi,
    ILogger<ValidadorTicketGlpi> logger) : IValidadorTicketGlpi
{
    /// <summary>
    /// Tope para comprobar todos los tickets de un envío. Se queda por debajo de los 20 s que
    /// espera el navegador (axios) a propósito: pasarse significa que el usuario ve un error
    /// mientras el servidor sigue y termina creando el envío igual.
    /// </summary>
    private static readonly TimeSpan Presupuesto = TimeSpan.FromSeconds(10);

    public async Task<Result> ValidarVariosAsync(
        IReadOnlyCollection<string> numerosTicket, CancellationToken ct = default)
    {
        if (numerosTicket is null || numerosTicket.Count == 0) return Result.Success();

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(Presupuesto);

        try
        {
            var resultados = await Task.WhenAll(
                numerosTicket.Select(x => ValidarAsync(x, limite.Token)));
            return Array.Find(resultados, x => x.IsFailure) ?? Result.Success();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Se agotó el presupuesto, no lo canceló quien llamó. Misma política que una caída de
            // GLPI: se deja pasar y queda el aviso, porque bloquear aquí pararía la operación de
            // todas las filiales por una mesa de ayuda lenta.
            logger.LogWarning(
                "La comprobación de {Cuantos} ticket(s) contra GLPI superó los {Segundos}s. Se dejan pasar sin validar.",
                numerosTicket.Count, Presupuesto.TotalSeconds);
            return Result.Success();
        }
    }

    public Task<Result> ValidarAsync(string numeroTicket, CancellationToken ct = default) =>
        ComprobarAsync(numeroTicket, numeroSerie: null, ct);

    public Task<Result> ValidarParaEquipoAsync(
        string numeroTicket, string? numeroSerie, CancellationToken ct = default) =>
        ComprobarAsync(numeroTicket, numeroSerie, ct);

    /// <summary>
    /// Las dos comprobaciones sobre una sola consulta del ticket.
    /// </summary>
    /// <remarks>
    /// Antes ValidarParaEquipoAsync llamaba a ValidarAsync y luego volvía a pedir el mismo ticket
    /// para mirar su equipo: dos viajes a GLPI para la misma información.
    ///
    /// <paramref name="numeroSerie"/> nulo significa que no hay con qué comparar, así que la
    /// comprobación de coherencia se salta y no se gasta la segunda llamada.
    /// </remarks>
    private async Task<Result> ComprobarAsync(
        string numeroTicket, string? numeroSerie, CancellationToken ct)
    {
        var ticket = numeroTicket?.Trim();
        // El formato ya lo cubren los validadores de cada request; esto es la red de abajo, para
        // que un llamador nuevo no termine pidiéndole a GLPI un id que no es un número.
        if (!NumeroTicket.EsValido(ticket))
            return Result.Failure(NumeroTicket.MensajeFormato, ErrorType.Validation);

        // El campo admite hasta 50 dígitos pero un id de GLPI es un entero: un número más largo
        // no puede existir allá. Se trata como los demás fallos de la integración —se deja pasar
        // y se avisa— en vez de bloquear por un formato que los validadores sí aceptan.
        if (!int.TryParse(ticket, out var id) || id <= 0)
        {
            logger.LogWarning(
                "El ticket {Ticket} no cabe en un id de GLPI, así que no se pudo verificar. Se deja pasar.",
                ticket);
            return Result.Success();
        }

        var consulta = await glpi.ObtenerTicketAsync(id, ct);

        if (consulta.IsFailure)
        {
            // Política acordada: sin respuesta de GLPI se deja pasar. Es también el único hueco de
            // la regla de un equipo por ticket, porque con GLPI caído no se puede contar. Se acepta
            // a sabiendas: bloquear pararía los envíos de las 34 filiales por un mal rato de la
            // mesa de ayuda.
            logger.LogWarning(
                "No se pudo verificar el ticket {Ticket} contra GLPI ({Motivo}). Se deja pasar sin validar.",
                id, consulta.Error);
            return Result.Success();
        }

        var enGlpi = consulta.Value!;
        if (!enGlpi.Existe)
            return Result.Failure($"El ticket {id} no existe en la mesa de ayuda.", ErrorType.Validation);

        // Antes que el conteo de equipos: un ticket cerrado no se arregla separando activos, y
        // decirle a la persona que reparta los equipos cuando el problema es el estado la manda
        // a hacer un trabajo que no resuelve nada.
        if (enGlpi.ElEstadoLoImpide)
        {
            logger.LogInformation(
                "Se rechazó el ticket {Ticket}: en GLPI está en estado {Estado}.", id, enGlpi.Estado);

            return Result.Failure(EstadoTicketGlpi.Mensaje(id, enGlpi.Estado), ErrorType.Validation);
        }

        if (!enGlpi.EsUsable)
        {
            // Se dice cuántos son y dónde se arregla: el cambio va en GLPI, no en el formulario.
            // Sin esa frase la persona intenta corregirlo aquí, no puede, y llama a soporte.
            logger.LogInformation(
                "Se rechazó el ticket {Ticket}: tiene {Cuantos} equipos asociados en GLPI.",
                id, enGlpi.Equipos.Count);

            return Result.Failure(
                $"El ticket {id} tiene {enGlpi.Equipos.Count} equipos asociados en la mesa de ayuda " +
                "y un ticket solo puede traer uno. Separe los equipos en tickets distintos en GLPI " +
                "y vuelva a intentarlo.",
                ErrorType.Validation);
        }

        if (string.IsNullOrWhiteSpace(numeroSerie) || enGlpi.EquipoUnico is not { } referencia)
            return Result.Success();

        return await CoincideConElEquipoAsync(id, referencia, numeroSerie, ct);
    }

    /// <summary>
    /// Que el ticket sea de ESTE equipo y no de otro. Solo rechaza la contradicción demostrada:
    /// si no se puede comparar, pasa.
    /// </summary>
    private async Task<Result> CoincideConElEquipoAsync(
        int id, ItemDeTicketGlpi referencia, string numeroSerie, CancellationToken ct)
    {
        var equipo = await glpi.ObtenerEquipoAsync(referencia.ItemType, referencia.ItemsId, ct);
        if (equipo.IsFailure) return Result.Success();

        var serialEnGlpi = equipo.Value?.Serial;
        if (string.IsNullOrWhiteSpace(serialEnGlpi)) return Result.Success();

        if (string.Equals(serialEnGlpi.Trim(), numeroSerie.Trim(), StringComparison.OrdinalIgnoreCase))
            return Result.Success();

        logger.LogInformation(
            "Se rechazó el ticket {Ticket} para el equipo con serial {Serie}: en GLPI es del serial {Otro}.",
            id, numeroSerie, serialEnGlpi);

        return Result.Failure(
            $"El ticket {id} es de otro equipo en la mesa de ayuda, serial {serialEnGlpi}. " +
            "Un ticket solo puede traer un equipo.",
            ErrorType.Validation);
    }
}
