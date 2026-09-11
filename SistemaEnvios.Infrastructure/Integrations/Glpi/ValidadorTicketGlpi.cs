using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
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

    public async Task<Result> ValidarAsync(string numeroTicket, CancellationToken ct = default)
    {
        var ticket = numeroTicket?.Trim();
        // El formato ya lo cubren los validadores de cada request; esto es la red de abajo, para
        // que un llamador nuevo no termine pidiéndole a GLPI un id que no es un número.
        if (!NumeroTicket.EsValido(ticket))
        {
            return Result.Failure(NumeroTicket.MensajeFormato, ErrorType.Validation);
        }

        // El campo admite hasta 50 dígitos pero un id de GLPI es un entero: un número más largo
        // no puede existir allá. Antes esto se colaba como "no es un número entero positivo",
        // un mensaje que no describía la restricción real. Se trata como los demás fallos de la
        // integración —se deja pasar y se avisa— en vez de bloquear el envío por un formato que
        // los validadores sí aceptan.
        if (!int.TryParse(ticket, out var id) || id <= 0)
        {
            logger.LogWarning(
                "El ticket {Ticket} no cabe en un id de GLPI, así que no se pudo verificar. Se deja pasar.",
                ticket);
            return Result.Success();
        }

        var resultado = await glpi.ObtenerTicketAsync(id, ct);

        if (resultado.IsFailure)
        {
            // Política acordada: sin respuesta de GLPI se deja pasar. Queda el aviso para que la
            // integración caída sea visible en el log y no un silencio que nadie nota.
            //
            // Es también el único hueco de la regla de un equipo por ticket: con GLPI caído no se
            // puede saber cuántos tiene. Se acepta a sabiendas, porque la alternativa —bloquear—
            // para los envíos de las 34 filiales cada vez que la mesa de ayuda tenga un mal rato.
            logger.LogWarning(
                "No se pudo verificar el ticket {Ticket} contra GLPI ({Motivo}). Se deja pasar sin validar.",
                id, resultado.Error);
            return Result.Success();
        }

        var ticketGlpi = resultado.Value!;
        if (!ticketGlpi.Existe)
            return Result.Failure($"El ticket {id} no existe en la mesa de ayuda.", ErrorType.Validation);

        if (!ticketGlpi.EsUsable)
        {
            // Se nombran los equipos para que quien lo lea sepa qué separar, y se dice dónde se
            // arregla: el cambio va en GLPI, no en este formulario. Sin eso, la persona intenta
            // corregirlo aquí y no puede.
            logger.LogInformation(
                "Se rechazó el ticket {Ticket}: tiene {Cuantos} equipos asociados en GLPI.",
                id, ticketGlpi.Equipos.Count);

            return Result.Failure(
                $"El ticket {id} tiene {ticketGlpi.Equipos.Count} equipos asociados en la mesa de ayuda " +
                "y un ticket solo puede traer uno. Separe los equipos en tickets distintos en GLPI " +
                "y vuelva a intentarlo.",
                ErrorType.Validation);
        }

        return Result.Success();
    }

    public async Task<Result> ValidarParaEquipoAsync(
        string numeroTicket, string? numeroSerie, CancellationToken ct = default)
    {
        var usable = await ValidarAsync(numeroTicket, ct);
        if (usable.IsFailure) return usable;

        if (!int.TryParse(numeroTicket?.Trim(), out var id) || id <= 0) return Result.Success();

        var consulta = await glpi.ObtenerTicketAsync(id, ct);
        if (consulta.IsFailure || !consulta.Value!.Existe) return Result.Success();

        if (consulta.Value.EquipoUnico is not { } referencia) return Result.Success();

        var equipo = await glpi.ObtenerEquipoAsync(referencia.ItemType, referencia.ItemsId, ct);
        if (equipo.IsFailure) return Result.Success();

        var serialEnGlpi = equipo.Value?.Serial;
        if (string.IsNullOrWhiteSpace(serialEnGlpi) || string.IsNullOrWhiteSpace(numeroSerie))
            return Result.Success();

        if (!string.Equals(serialEnGlpi.Trim(), numeroSerie.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Se rechazó el ticket {Ticket} para el equipo con serial {Serie}: en GLPI es del serial {Otro}.",
                id, numeroSerie, serialEnGlpi);

            return Result.Failure(
                $"El ticket {id} es de otro equipo en la mesa de ayuda, serial {serialEnGlpi}. " +
                "Un ticket solo puede traer un equipo.",
                ErrorType.Validation);
        }

        return Result.Success();
    }
}
