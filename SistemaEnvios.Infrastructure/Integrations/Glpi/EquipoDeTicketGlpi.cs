using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <inheritdoc cref="IEquipoDeTicketGlpi"/>
public sealed class EquipoDeTicketGlpi(
    IGlpiClient glpi,
    SistemaEnviosDbContext db,
    ILogger<EquipoDeTicketGlpi> logger) : IEquipoDeTicketGlpi
{
    /// <summary>
    /// Solo estos tipos: son los que sabemos leer y los que corresponden al inventario que viaja.
    /// De un Contract o un Software no se sacaría nada útil y sí una llamada desperdiciada.
    /// </summary>
    private static readonly HashSet<string> TiposSoportados =
        new(StringComparer.OrdinalIgnoreCase) { "Computer", "Monitor", "Printer" };

    public async Task<Result<EquipoDeTicketResponse>> ObtenerAsync(
        string numeroTicket, CancellationToken ct = default)
    {
        var ticket = numeroTicket?.Trim();
        if (!NumeroTicket.EsValido(ticket) || !int.TryParse(ticket, out var id) || id <= 0)
            return Result<EquipoDeTicketResponse>.Failure(NumeroTicket.MensajeFormato, ErrorType.Validation);

        // Primero nuestra base. Si el ticket ya abrió un caso no hay nada que autocompletar, y
        // preguntarle a GLPI sería gastar dos llamadas para un ticket que se va a rechazar igual.
        if (await db.EnvioEquipos.AsNoTracking()
                .AnyAsync(x => x.EnvioEquipoOrigenId == null && x.NumeroTicket == ticket, ct))
        {
            return Result<EquipoDeTicketResponse>.Failure(
                $"El ticket {ticket} ya fue utilizado para abrir otro caso.", ErrorType.Conflict);
        }

        var consulta = await glpi.ObtenerTicketAsync(id, ct);
        if (consulta.IsFailure)
            return Result<EquipoDeTicketResponse>.Failure(consulta.Error!, consulta.ErrorType);

        var enGlpi = consulta.Value!;
        if (!enGlpi.Existe)
        {
            return Result<EquipoDeTicketResponse>.Failure(
                $"El ticket {id} no existe en la mesa de ayuda.", ErrorType.Validation);
        }

        // Mismo mensaje que al guardar: si aquí dijera otra cosa, el usuario creería que son dos
        // problemas distintos.
        if (!enGlpi.EsUsable)
        {
            return Result<EquipoDeTicketResponse>.Failure(
                $"El ticket {id} tiene {enGlpi.Equipos.Count} equipos asociados en la mesa de ayuda " +
                "y un ticket solo puede traer uno. Separe los equipos en tickets distintos en GLPI " +
                "y vuelva a intentarlo.",
                ErrorType.Validation);
        }

        if (enGlpi.EquipoUnico is not { } referencia)
            return Result<EquipoDeTicketResponse>.Success(new EquipoDeTicketResponse(id, null));

        if (!TiposSoportados.Contains(referencia.ItemType))
        {
            logger.LogInformation(
                "El ticket {Ticket} trae un {Tipo}, que no se autocompleta.", id, referencia.ItemType);
            return Result<EquipoDeTicketResponse>.Success(new EquipoDeTicketResponse(id, null));
        }

        var equipo = await glpi.ObtenerEquipoAsync(referencia.ItemType, referencia.ItemsId, ct);
        if (equipo.IsFailure)
            return Result<EquipoDeTicketResponse>.Failure(equipo.Error!, equipo.ErrorType);

        if (equipo.Value is not { } datos || datos.EnPapeleraOPlantilla)
            return Result<EquipoDeTicketResponse>.Success(new EquipoDeTicketResponse(id, null));

        return Result<EquipoDeTicketResponse>.Success(
            new EquipoDeTicketResponse(id, await MapearAsync(datos, ct)));
    }

    private async Task<EquipoAutocompletado> MapearAsync(EquipoGlpi datos, CancellationToken ct)
    {
        // Un equipo que ya viaja en otro envío no se puede asociar a este. Se avisa aquí para que
        // el usuario lo sepa antes de llenar el formulario y no al guardarlo.
        var reservado = datos.Serial is not null
            && await db.ReservasEquipoEnvio.AsNoTracking()
                .AnyAsync(r => db.Equipos.Any(e => e.EquipoId == r.EquipoId && e.NumeroSerie == datos.Serial), ct);

        return new EquipoAutocompletado(
            Marca: datos.Marca,
            Modelo: datos.Modelo,
            Serial: datos.Serial,
            // El código de activo es numérico en ADRTrack; el otherserial de GLPI es texto libre.
            CodigoActivo: EsNumerico(datos.CodigoActivo) ? datos.CodigoActivo : null,
            TipoEquipoId: await ResolverTipoAsync(datos, ct),
            Nombre: datos.Nombre,
            YaEstaEnOtroEnvio: reservado);
    }

    private async Task<int?> ResolverTipoAsync(EquipoGlpi datos, CancellationToken ct)
    {
        var nombre = NombreDeTipo(datos);
        var tipo = await db.TiposEquipo.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Nombre == nombre, ct);
        return tipo?.TipoEquipoId;
    }

    /// <summary>
    /// El vocabulario de GLPI no es el nuestro: sus tipos de computadora son "Low Profile
    /// Desktop", "Mini Tower", "Notebook". Lo que no se reconoce cae en "Otro" en vez de quedar
    /// vacío, para que el usuario vea algo y lo corrija si hace falta.
    /// </summary>
    private static string NombreDeTipo(EquipoGlpi datos)
    {
        if (datos.ItemType.Equals("Monitor", StringComparison.OrdinalIgnoreCase)) return "Monitor";
        if (datos.ItemType.Equals("Printer", StringComparison.OrdinalIgnoreCase)) return "Impresora";
        if (!datos.ItemType.Equals("Computer", StringComparison.OrdinalIgnoreCase)) return "Otro";

        var tipo = datos.TipoGlpi ?? string.Empty;
        var esPortatil = tipo.Contains("laptop", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("notebook", StringComparison.OrdinalIgnoreCase)
            || tipo.Contains("portable", StringComparison.OrdinalIgnoreCase);

        return esPortatil ? "Laptop" : "Computadora de escritorio";
    }

    private static bool EsNumerico(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && valor.All(char.IsAsciiDigit);
}
