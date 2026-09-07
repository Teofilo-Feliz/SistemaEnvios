using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <inheritdoc cref="IValidadorTicketGlpi"/>
public sealed class ValidadorTicketGlpi(
    IGlpiClient glpi,
    ILogger<ValidadorTicketGlpi> logger) : IValidadorTicketGlpi
{
    public async Task<Result> ValidarAsync(string numeroTicket, CancellationToken ct = default)
    {
        var ticket = numeroTicket?.Trim();
        // El formato ya lo cubren los validadores de cada request; esto es la red de abajo, para
        // que un llamador nuevo no termine pidiéndole a GLPI un id que no es un número.
        if (!int.TryParse(ticket, out var id) || id <= 0)
        {
            return Result.Failure(
                "El número de ticket debe ser un número entero positivo.", ErrorType.Validation);
        }

        var resultado = await glpi.ItemExistsAsync("Ticket", id, ct);

        if (resultado.IsFailure)
        {
            // Política acordada: sin respuesta de GLPI se deja pasar. Queda el aviso para que la
            // integración caída sea visible en el log y no un silencio que nadie nota.
            logger.LogWarning(
                "No se pudo verificar el ticket {Ticket} contra GLPI ({Motivo}). Se deja pasar sin validar.",
                id, resultado.Error);
            return Result.Success();
        }

        return resultado.Value
            ? Result.Success()
            : Result.Failure(
                $"El ticket {id} no existe en la mesa de ayuda.", ErrorType.Validation);
    }
}
