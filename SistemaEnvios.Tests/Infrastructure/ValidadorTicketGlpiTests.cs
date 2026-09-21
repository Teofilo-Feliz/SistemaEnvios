using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Integraciones;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Infrastructure.Integrations.Glpi;

namespace SistemaEnvios.Tests.Infrastructure;

/// <summary>
/// La política acordada: un ticket que GLPI no tiene se rechaza, pero GLPI caído deja pasar.
/// Es la diferencia que hace que una caída de la mesa de ayuda no paralice a las filiales, y es
/// justo la que se rompería sin ruido si alguien "simplifica" el manejo de errores.
///
/// Y la política del negocio: un ticket, un equipo. En ADRTrack el ticket identifica el caso de
/// UN equipo —el índice único UX_EnvioEquipos_TicketApertura lo sostiene en la base—, así que un
/// ticket que en GLPI arrastra varios no sirve y se corta aquí, antes de llenar el formulario.
/// </summary>
public sealed class ValidadorTicketGlpiTests
{
    [Fact]
    public async Task DejaPasarUnTicketConUnSoloEquipo()
    {
        var validador = Validador(ConEquipos(("Computer", 657)));

        var resultado = await validador.ValidarAsync("25000");

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    /// <summary>
    /// Sin equipos también pasa: es el ticket que alguien abrió y todavía no le colgó el activo.
    /// La persona escribe marca, modelo y serial a mano, que es justo lo que hacía siempre.
    /// </summary>
    [Fact]
    public async Task DejaPasarUnTicketSinEquipos()
    {
        var validador = Validador(ConEquipos());

        var resultado = await validador.ValidarAsync("25000");

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task RechazaUnTicketConVariosEquipos()
    {
        var validador = Validador(ConEquipos(("Computer", 657), ("Computer", 812), ("Monitor", 44)));

        var resultado = await validador.ValidarAsync("30261");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains("3 equipos asociados", resultado.Error);
    }

    /// <summary>
    /// El mensaje tiene que decir dónde se arregla. El cambio va en GLPI, no en este formulario:
    /// sin esa frase la persona intenta corregirlo aquí, no puede, y llama a soporte.
    /// </summary>
    [Fact]
    public async Task ElRechazoDiceQueSeArreglaEnGlpi()
    {
        var validador = Validador(ConEquipos(("Computer", 1), ("Computer", 2)));

        var resultado = await validador.ValidarAsync("30261");

        Assert.Contains("GLPI", resultado.Error);
    }

    [Fact]
    public async Task RechazaUnTicketQueGlpiNoTiene()
    {
        var validador = Validador(TicketGlpi.NoEncontrado);

        var resultado = await validador.ValidarAsync("99999999");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains("no existe en la mesa de ayuda", resultado.Error);
    }

    [Fact]
    public async Task ConGlpiCaidoDejaPasarEnVezDeBloquearLaOperacion()
    {
        var validador = new ValidadorTicketGlpi(
            new GlpiFalso(Result<TicketGlpi>.Failure("GLPI no responde.", ErrorType.ExternalService)),
            NullLogger<ValidadorTicketGlpi>.Instance);

        var resultado = await validador.ValidarAsync("25000");

        Assert.True(resultado.IsSuccess);
    }

    /// <summary>
    /// El hueco conocido de la regla: con GLPI caído no se puede contar los equipos, así que un
    /// ticket de varios pasaría. Se acepta a sabiendas —bloquear pararía a las 34 filiales— y se
    /// fija aquí para que sea una decisión documentada y no una sorpresa.
    /// </summary>
    [Fact]
    public async Task ConGlpiCaidoNoSePuedeAplicarLaReglaDeUnEquipo()
    {
        var validador = new ValidadorTicketGlpi(
            new GlpiFalso(Result<TicketGlpi>.Failure("Circuito abierto.", ErrorType.ExternalService)),
            NullLogger<ValidadorTicketGlpi>.Instance);

        Assert.True((await validador.ValidarAsync("30261")).IsSuccess);
    }

    [Fact]
    public async Task ValidarVarios_DevuelveElPrimerRechazo()
    {
        var validador = Validador(ConEquipos(("Computer", 1), ("Computer", 2)));

        var resultado = await validador.ValidarVariosAsync(["25000", "30261"]);

        Assert.True(resultado.IsFailure);
        Assert.Contains("2 equipos asociados", resultado.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task RechazaLoQueNoEsUnNumeroDeTicket(string ticket)
    {
        // El cliente ni se llama: falla antes, así que se le pasa uno que explotaría si lo usara.
        var validador = new ValidadorTicketGlpi(new GlpiQueFalla(), NullLogger<ValidadorTicketGlpi>.Instance);

        var resultado = await validador.ValidarAsync(ticket);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
    }

    /// <summary>
    /// La regla de la ADR: solo se estrena un envío con un ticket que un técnico ya tomó. Un
    /// caso cerrado o resuelto está terminado, y uno que nadie ha tomado todavía no justifica
    /// mover un equipo.
    /// </summary>
    [Theory]
    [InlineData(1, "Nuevo")]
    [InlineData(3, "En curso (planificado)")]
    [InlineData(4, "En espera")]
    [InlineData(5, "Resuelto")]
    [InlineData(6, "Cerrado")]
    public async Task RechazaUnTicketQueNoEstaEnCurso(int estado, string nombre)
    {
        var validador = Validador(ConEquipos(estado, ("Computer", 657)));

        var resultado = await validador.ValidarAsync("30261");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains(nombre, resultado.Error);
    }

    /// <summary>
    /// Un ticket cerrado Y con varios equipos se rechaza por el estado. Si ganara el conteo, el
    /// mensaje mandaría a la persona a repartir activos en GLPI, trabajo que no arreglaría nada
    /// porque el caso ya está cerrado.
    /// </summary>
    [Fact]
    public async Task ElEstadoPesaMasQueElConteoDeEquipos()
    {
        var validador = Validador(ConEquipos(6, ("Computer", 1), ("Computer", 2)));

        var resultado = await validador.ValidarAsync("30261");

        Assert.Contains("Cerrado", resultado.Error);
        Assert.DoesNotContain("2 equipos asociados", resultado.Error);
    }

    /// <summary>
    /// Estado nulo es "no se pudo leer", no "está mal", y deja pasar. Es el mismo criterio que
    /// con GLPI caído: si una versión de la mesa de ayuda dejara de mandar el campo, la regla se
    /// apagaría sola en vez de bloquear a las 34 filiales sin que nadie entienda por qué.
    /// </summary>
    [Fact]
    public async Task SinEstadoLegibleDejaPasar()
    {
        var validador = Validador(new TicketGlpi(true, [new ItemDeTicketGlpi("Computer", 657)]));

        Assert.True((await validador.ValidarAsync("25000")).IsSuccess);
    }

    private static TicketGlpi ConEquipos(params (string Tipo, int Id)[] equipos) =>
        ConEquipos(EstadoTicketGlpi.EnCurso, equipos);

    private static TicketGlpi ConEquipos(int estado, params (string Tipo, int Id)[] equipos) =>
        new(true, [.. equipos.Select(x => new ItemDeTicketGlpi(x.Tipo, x.Id))], estado);

    private static ValidadorTicketGlpi Validador(TicketGlpi respuesta) =>
        new(new GlpiFalso(Result<TicketGlpi>.Success(respuesta)), NullLogger<ValidadorTicketGlpi>.Instance);

    private sealed class GlpiFalso(Result<TicketGlpi> respuesta) : IGlpiClient
    {
        public Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default) =>
            Task.FromResult(respuesta);

        public Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default) =>
            throw new InvalidOperationException("El validador no lee activos: solo cuenta cuántos hay.");
    }

    private sealed class GlpiQueFalla : IGlpiClient
    {
        public Task<Result<TicketGlpi>> ObtenerTicketAsync(int ticketId, CancellationToken ct = default) =>
            throw new InvalidOperationException("No debió consultarse a GLPI con un ticket inválido.");

        public Task<Result<EquipoGlpi?>> ObtenerEquipoAsync(string itemType, int id, CancellationToken ct = default) =>
            throw new InvalidOperationException("No debió consultarse a GLPI con un ticket inválido.");
    }
}
