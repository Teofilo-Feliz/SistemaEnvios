using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// Doble del validador de tickets para las pruebas que no van sobre GLPI. Por omisión acepta
/// todo, para que las pruebas de alcance, estados y creación sigan midiendo lo suyo; quien
/// necesite el rechazo lo construye con <see cref="QueRechaza"/>.
/// </summary>
public sealed class ValidadorTicketDePrueba(Result? respuesta = null) : IValidadorTicketGlpi
{
    public static ValidadorTicketDePrueba QueAcepta() => new();

    public static ValidadorTicketDePrueba QueRechaza(string mensaje = "El ticket no existe en la mesa de ayuda.") =>
        new(Result.Failure(mensaje, ErrorType.Validation));

    public Task<Result> ValidarAsync(string numeroTicket, CancellationToken ct = default) =>
        Task.FromResult(respuesta ?? Result.Success());

    public Task<Result> ValidarVariosAsync(
        IReadOnlyCollection<string> numerosTicket, CancellationToken ct = default) =>
        Task.FromResult(respuesta ?? Result.Success());
}
