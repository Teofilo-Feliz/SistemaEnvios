using System.Net;
using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <inheritdoc cref="IGlpiClient"/>
public sealed class GlpiClient(
    HttpClient http,
    GlpiSessionProvider sesion,
    ILogger<GlpiClient> logger) : IGlpiClient
{
    private const int LargoMaximoItemType = 50;

    public async Task<Result<bool>> ItemExistsAsync(string itemType, int id, CancellationToken ct = default)
    {
        if (!EsItemTypeValido(itemType))
        {
            return Result<bool>.Failure(
                "El tipo de item de GLPI solo admite letras, dígitos y guion bajo, y debe empezar con letra.",
                ErrorType.Validation);
        }

        if (id <= 0)
            return Result<bool>.Failure("El id del item debe ser mayor que cero.", ErrorType.Validation);

        try
        {
            var estado = await ConsultarAsync(itemType, id, reintentarSesion: true, ct).ConfigureAwait(false);
            return estado switch
            {
                HttpStatusCode.OK => Result<bool>.Success(true),
                HttpStatusCode.NotFound => Result<bool>.Success(false),
                // 400 suele ser un itemtype que GLPI no conoce: es culpa de quien llamó, no de GLPI.
                HttpStatusCode.BadRequest => Result<bool>.Failure(
                    $"GLPI no reconoce el tipo de item '{itemType}'.", ErrorType.Validation),
                _ => Fallo($"GLPI respondió {(int)estado} al consultar {itemType}/{id}.")
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Canceló quien llamó (cliente cerró la conexión): no es una falla de GLPI.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Timeout consultando {ItemType}/{Id} en GLPI.", itemType, id);
            return Fallo("La mesa de ayuda no respondió a tiempo.");
        }
        catch (GlpiIntegrationException ex)
        {
            logger.LogError(ex, "Falla de integración con GLPI consultando {ItemType}/{Id}.", itemType, id);
            return Fallo(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "No se pudo contactar a GLPI para consultar {ItemType}/{Id}.", itemType, id);
            return Fallo("No se pudo contactar a la mesa de ayuda.");
        }
        catch (Exception ex)
        {
            // Frontera con un sistema externo: nada de lo que venga de ahí puede tumbar el API.
            // Incluye las excepciones de Polly (circuito abierto, timeout de la estrategia), que
            // no se nombran aquí para no acoplar Infrastructure a sus tipos.
            logger.LogError(ex, "Error inesperado consultando {ItemType}/{Id} en GLPI.", itemType, id);
            return Fallo("La mesa de ayuda no está disponible.");
        }
    }

    private async Task<HttpStatusCode> ConsultarAsync(
        string itemType, int id, bool reintentarSesion, CancellationToken ct)
    {
        var token = await sesion.ObtenerTokenAsync(ct).ConfigureAwait(false);
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"{itemType}/{id}");
        peticion.Headers.TryAddWithoutValidation("Session-Token", token);

        using var respuesta = await http.SendAsync(peticion, ct).ConfigureAwait(false);

        // El token pudo morir antes de nuestro TTL (GLPI reiniciado, sesión cerrada desde otro
        // lado). Se descarta y se reintenta una sola vez con sesión nueva; sin el tope, un 401
        // permanente por credenciales malas se volvería un bucle infinito.
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized && reintentarSesion)
        {
            logger.LogInformation("GLPI rechazó el session_token; se abre una sesión nueva y se reintenta.");
            sesion.Invalidar();
            return await ConsultarAsync(itemType, id, reintentarSesion: false, ct).ConfigureAwait(false);
        }

        return respuesta.StatusCode;
    }

    private Result<bool> Fallo(string mensaje) =>
        Result<bool>.Failure(mensaje, ErrorType.ExternalService);

    /// <summary>
    /// El itemType se concatena a la URL, así que se restringe al alfabeto real de GLPI
    /// ("Ticket", "Computer", "Item_DeviceProcessor"). Sin esto, una barra o un ".." en el
    /// parámetro reescribiría la ruta de la petición.
    /// </summary>
    private static bool EsItemTypeValido(string itemType)
    {
        if (string.IsNullOrEmpty(itemType) || itemType.Length > LargoMaximoItemType) return false;
        if (!char.IsAsciiLetter(itemType[0])) return false;
        foreach (var c in itemType)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }
}
