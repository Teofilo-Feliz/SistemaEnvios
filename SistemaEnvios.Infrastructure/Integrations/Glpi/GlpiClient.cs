using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <inheritdoc cref="IGlpiClient"/>
public sealed class GlpiClient(
    HttpClient http,
    GlpiSessionProvider sesion,
    IMemoryCache cache,
    ILogger<GlpiClient> logger) : IGlpiClient
{
    private const int LargoMaximoItemType = 50;

    /// <summary>
    /// Cuánto se reutiliza una respuesta de GLPI.
    /// </summary>
    /// <remarks>
    /// El mismo ticket se consulta dos veces seguidas: una al autocompletar el formulario y otra
    /// al guardar, cuando el servidor vuelve a comprobarlo por su cuenta. Con la caché, la segunda
    /// no sale a la red.
    ///
    /// Corto a propósito. Si alguien le cuelga un segundo equipo al ticket en GLPI, no queremos
    /// estar sirviendo un "está bien" de hace media hora: un minuto cubre el trecho entre buscar y
    /// guardar y poco más.
    /// </remarks>
    private static readonly TimeSpan VigenciaConsulta = TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public async Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default)
    {
        if (ticketId <= 0)
            return Result<TicketGlpi>.Failure("El id del ticket debe ser mayor que cero.", ErrorType.Validation);

        var clave = $"glpi-ticket::{ticketId}";
        if (cache.TryGetValue(clave, out TicketGlpi? cacheado) && cacheado is not null)
            return Result<TicketGlpi>.Success(cacheado);

        try
        {
            // El estado solo lo trae el ticket y los activos solo Item_Ticket, así que hacen
            // falta las dos. Van a la vez porque en serie el usuario esperaría una detrás de otra
            // cada vez que teclea un número.
            var ticket = LeerAsync($"Ticket/{ticketId}", reintentarSesion: true, ct);
            // Sin expand_dropdowns a propósito: con él, items_id deja de ser el id y pasa a ser el
            // hostname del equipo, y la segunda llamada se queda sin a dónde ir.
            var items = LeerAsync($"Ticket/{ticketId}/Item_Ticket", reintentarSesion: true, ct);
            await Task.WhenAll(ticket, items).ConfigureAwait(false);

            var (codigoTicket, cuerpoTicket) = await ticket.ConfigureAwait(false);
            var (codigoItems, cuerpoItems) = await items.ConfigureAwait(false);

            // Solo se guarda lo que GLPI respondió de verdad. Un fallo no se cachea: sería
            // convertir un tropiezo de red en un minuto de rechazos.
            if (codigoTicket == HttpStatusCode.NotFound)
                return Result<TicketGlpi>.Success(Recordar(clave, TicketGlpi.NoEncontrado));

            if (codigoTicket != HttpStatusCode.OK)
                return FalloTicket($"GLPI respondió {(int)codigoTicket} al consultar el ticket {ticketId}.");

            if (codigoItems is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
                return FalloTicket($"GLPI respondió {(int)codigoItems} al consultar los equipos del ticket {ticketId}.");

            // Un 404 en Item_Ticket con el ticket vivo no debería ocurrir —GLPI devuelve el
            // arreglo vacío cuando no hay activos—, pero si ocurriera, "sin equipos" es lo que
            // describe la respuesta.
            List<ItemDeTicketGlpi> equipos = codigoItems == HttpStatusCode.OK ? LeerItems(cuerpoItems) : [];

            return Result<TicketGlpi>.Success(
                Recordar(clave, new TicketGlpi(true, equipos, LeerEstado(cuerpoTicket))));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Timeout consultando los equipos del ticket {Ticket} en GLPI.", ticketId);
            return FalloTicket("La mesa de ayuda no respondió a tiempo.");
        }
        catch (GlpiIntegrationException ex)
        {
            logger.LogError(ex, "Falla de integración consultando los equipos del ticket {Ticket}.", ticketId);
            return FalloTicket(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "No se pudo contactar a GLPI para el ticket {Ticket}.", ticketId);
            return FalloTicket("No se pudo contactar a la mesa de ayuda.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inesperado consultando los equipos del ticket {Ticket}.", ticketId);
            return FalloTicket("La mesa de ayuda no está disponible.");
        }
    }

    /// <summary>
    /// Item_Ticket devuelve un arreglo de asociaciones. Se toma solo lo que se necesita para ir a
    /// buscar el activo: de qué tipo es y su id.
    /// </summary>
    /// <remarks>
    /// Tolerante a propósito. Un cuerpo que no sea el arreglo esperado —GLPI devuelve arreglos de
    /// texto para sus errores, por ejemplo ["ERROR_ITEM_NOT_FOUND", "Elemento no encontrado"]— se
    /// lee como "sin equipos" en vez de reventar. Y una entrada sin itemtype o con items_id en
    /// cero se descarta: no se puede ir a buscar un activo con eso.
    /// </remarks>
    private static List<ItemDeTicketGlpi> LeerItems(string cuerpo)
    {
        List<ItemDeTicketGlpi> items = [];
        if (string.IsNullOrWhiteSpace(cuerpo)) return items;

        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            if (json.RootElement.ValueKind != JsonValueKind.Array) return items;

            foreach (var entrada in json.RootElement.EnumerateArray())
            {
                if (entrada.ValueKind != JsonValueKind.Object) continue;
                if (!entrada.TryGetProperty("itemtype", out var tipo) || tipo.ValueKind != JsonValueKind.String) continue;
                if (!entrada.TryGetProperty("items_id", out var id)) continue;

                var itemType = tipo.GetString();
                var itemsId = LeerEntero(id);
                if (string.IsNullOrWhiteSpace(itemType) || itemsId <= 0) continue;

                items.Add(new ItemDeTicketGlpi(itemType, itemsId));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return items;
    }

    /// <summary>
    /// GLPI manda los enteros unas veces como número y otras como texto, según la versión y el
    /// campo. Se aceptan los dos en vez de confiar en uno.
    /// </summary>
    private static int LeerEntero(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.Number when valor.TryGetInt32(out var numero) => numero,
        JsonValueKind.String when int.TryParse(valor.GetString(), out var texto) => texto,
        _ => 0
    };

/// <summary>
    /// El estado del ticket, tal como lo numera GLPI. Null cuando no vino o vino en cero: eso es
    /// "no se pudo leer", y quien decide lo trata como motivo para no bloquear.
    /// </summary>
    private static int? LeerEstado(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;

        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            if (json.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!json.RootElement.TryGetProperty("status", out var valor)) return null;

            var estado = LeerEntero(valor);
            return estado > 0 ? estado : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(
        string itemType, int id, CancellationToken ct = default)
    {
        if (!EsItemTypeValido(itemType))
        {
            return Result<EquipoGlpi?>.Failure(
                "El tipo de item de GLPI solo admite letras, dígitos y guion bajo, y debe empezar con letra.",
                ErrorType.Validation);
        }

        if (id <= 0)
            return Result<EquipoGlpi?>.Failure("El id del item debe ser mayor que cero.", ErrorType.Validation);

        var clave = $"glpi-equipo::{itemType}::{id}";
        if (cache.TryGetValue(clave, out EquipoGlpi? cacheado))
            return Result<EquipoGlpi?>.Success(cacheado);

        try
        {
            var (estado, cuerpo) = await LeerAsync(
                $"{itemType}/{id}?expand_dropdowns=true", reintentarSesion: true, ct).ConfigureAwait(false);

            return estado switch
            {
                HttpStatusCode.OK => Result<EquipoGlpi?>.Success(Recordar(clave, LeerEquipo(itemType, cuerpo))),
                HttpStatusCode.NotFound => Result<EquipoGlpi?>.Success(Recordar<EquipoGlpi?>(clave, null)),
                _ => Result<EquipoGlpi?>.Failure(
                    $"GLPI respondió {(int)estado} al consultar {itemType}/{id}.", ErrorType.ExternalService)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo leer {ItemType}/{Id} de GLPI.", itemType, id);
            return Result<EquipoGlpi?>.Failure("La mesa de ayuda no está disponible.", ErrorType.ExternalService);
        }
    }

    /// <summary>
    /// El campo del modelo se llama distinto en cada tipo —computermodels_id, monitormodels_id,
    /// printermodels_id— mientras que serial y manufacturers_id se llaman igual en todos.
    /// </summary>
    private static EquipoGlpi? LeerEquipo(string itemType, string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;

        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            var raiz = json.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;

            var minusculas = itemType.ToLowerInvariant();

            return new EquipoGlpi(
                ItemType: itemType,
                Marca: LeerTexto(raiz, "manufacturers_id"),
                Modelo: LeerTexto(raiz, $"{minusculas}models_id"),
                Serial: LeerTexto(raiz, "serial"),
                CodigoActivo: LeerTexto(raiz, "otherserial"),
                Nombre: LeerTexto(raiz, "name"),
                TipoGlpi: LeerTexto(raiz, $"{minusculas}types_id"),
                EnPapeleraOPlantilla: EsVerdadero(raiz, "is_deleted") || EsVerdadero(raiz, "is_template"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Con expand_dropdowns, GLPI devuelve el nombre como texto cuando el dropdown tiene valor y
    /// el número 0 cuando está vacío. Un equipo sin fabricante llegaría como 0, no como "".
    /// </summary>
    private static string? LeerTexto(JsonElement objeto, string propiedad)
    {
        if (!objeto.TryGetProperty(propiedad, out var valor)) return null;

        var texto = valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.TryGetInt32(out var numero) && numero == 0 ? null : valor.ToString(),
            _ => null
        };

        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    private static bool EsVerdadero(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => valor.TryGetInt32(out var numero) && numero != 0,
            JsonValueKind.String => valor.GetString() is "1" or "true",
            _ => false
        };

    private async Task<(HttpStatusCode Estado, string Cuerpo)> LeerAsync(
        string ruta, bool reintentarSesion, CancellationToken ct)
    {
        var token = await sesion.ObtenerTokenAsync(ct).ConfigureAwait(false);
        using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
        peticion.Headers.TryAddWithoutValidation("Session-Token", token);

        using var respuesta = await http.SendAsync(peticion, ct).ConfigureAwait(false);

        if (respuesta.StatusCode == HttpStatusCode.Unauthorized && reintentarSesion)
        {
            logger.LogInformation("GLPI rechazó el session_token; se abre una sesión nueva y se reintenta.");
            sesion.Invalidar();
            return await LeerAsync(ruta, reintentarSesion: false, ct).ConfigureAwait(false);
        }

        // Solo se lee el cuerpo cuando hay algo que leer: en un error, GLPI devuelve su arreglo de
        // códigos y no nos sirve de nada.
        var cuerpo = respuesta.IsSuccessStatusCode
            ? await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
            : string.Empty;

        return (respuesta.StatusCode, cuerpo);
    }

    private Result<TicketGlpi> FalloTicket(string mensaje) =>
        Result<TicketGlpi>.Failure(mensaje, ErrorType.ExternalService);
    private T Recordar<T>(string clave, T valor)
    {
        cache.Set(clave, valor, VigenciaConsulta);
        return valor;
    }

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
