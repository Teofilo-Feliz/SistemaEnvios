using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Infrastructure.Integrations.Glpi;

namespace SistemaEnvios.Tests.Infrastructure;

/// <summary>
/// Corregir el ticket de una fila que ya existe exige confirmar que ese ticket es el de ESE equipo.
///
/// Sin esta regla se le podía poner a una máquina el ticket de otra y la fila quedaba diciendo que
/// su caso es un número que GLPI no relaciona con ella.
///
/// Rechaza la contradicción demostrada —que el ticket sea de otra máquina— y no la duda: un
/// ticket sin activo asociado pasa, porque al cambiar el ticket el formulario trae el equipo
/// nuevo o vacía los campos para escribirlos a mano.
/// </summary>
public sealed class ValidarTicketParaEquipoTests
{
    private const string Serial = "MXL04916TL";

    [Fact]
    public async Task AceptaElTicketDelMismoEquipo()
    {
        var validador = Validador(Ticket(("Computer", 657)), Equipo(Serial));

        var resultado = await validador.ValidarParaEquipoAsync("30261", Serial);

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task ElSerialSeComparaSinDistinguirMayusculas()
    {
        var validador = Validador(Ticket(("Computer", 657)), Equipo(Serial.ToLowerInvariant()));

        Assert.True((await validador.ValidarParaEquipoAsync("30261", Serial)).IsSuccess);
    }

    [Fact]
    public async Task RechazaElTicketDeOtroEquipo()
    {
        var validador = Validador(Ticket(("Computer", 812)), Equipo("OTRO-SERIAL"));

        var resultado = await validador.ValidarParaEquipoAsync("30500", Serial);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains("es de otro equipo", resultado.Error);
    }

    /// <summary>
    /// Un ticket sin activo asociado pasa: es el caso de quien abre el ticket antes de colgarle el
    /// equipo, y el formulario deja escribir los datos a mano.
    /// </summary>
    [Fact]
    public async Task DejaPasarUnTicketSinEquipoAsociado()
    {
        var validador = Validador(Ticket(), null);

        Assert.True((await validador.ValidarParaEquipoAsync("30500", Serial)).IsSuccess);
    }

    /// <summary>
    /// Sin serial en alguno de los dos lados no hay contradicción que demostrar, así que pasa.
    /// Se rechaza la contradicción probada, no la duda.
    /// </summary>
    [Theory]
    [InlineData(null, Serial)]
    [InlineData("", Serial)]
    [InlineData(Serial, null)]
    public async Task DejaPasarCuandoNoHaySerialConQueComparar(string? enGlpi, string? enLaFila)
    {
        var validador = Validador(Ticket(("Computer", 657)), Equipo(enGlpi));

        Assert.True((await validador.ValidarParaEquipoAsync("30261", enLaFila)).IsSuccess);
    }

    /// <summary>
    /// La excepción de siempre: sin respuesta de GLPI no se puede confirmar nada, y bloquear aquí
    /// dejaría a las filiales sin poder corregir un ticket mal tecleado.
    /// </summary>
    [Fact]
    public async Task ConGlpiCaidoDejaPasar()
    {
        var validador = new ValidadorTicketGlpi(
            new GlpiFalso(Result<TicketGlpi>.Failure("Circuito abierto.", ErrorType.ExternalService), null),
            NullLogger<ValidadorTicketGlpi>.Instance);

        Assert.True((await validador.ValidarParaEquipoAsync("30261", Serial)).IsSuccess);
    }

    /// <summary>Un ticket de varios equipos se rechaza antes, por la regla general.</summary>
    [Fact]
    public async Task RechazaUnTicketConVariosEquipos()
    {
        var validador = Validador(Ticket(("Computer", 1), ("Computer", 2)), Equipo(Serial));

        var resultado = await validador.ValidarParaEquipoAsync("30261", Serial);

        Assert.True(resultado.IsFailure);
        Assert.Contains("2 equipos asociados", resultado.Error);
    }

    private static TicketGlpi Ticket(params (string Tipo, int Id)[] equipos) =>
        new(true, [.. equipos.Select(x => new ItemDeTicketGlpi(x.Tipo, x.Id))], EstadoTicketGlpi.EnCurso);

    private static EquipoGlpi Equipo(string? serial) =>
        new("Computer", "HP", "Compaq", serial, null, "LAB-INFORMATICA", null, false);

    private static ValidadorTicketGlpi Validador(TicketGlpi ticket, EquipoGlpi? equipo) =>
        new(new GlpiFalso(Result<TicketGlpi>.Success(ticket), equipo),
            NullLogger<ValidadorTicketGlpi>.Instance);

    private sealed class GlpiFalso(Result<TicketGlpi> ticket, EquipoGlpi? equipo) : IGlpiClient
    {
        public Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default) =>
            Task.FromResult(ticket);

        public Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default) =>
            Task.FromResult(Result<EquipoGlpi?>.Success(equipo));
    }
}
