using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Infrastructure.Integrations.Glpi;

namespace SistemaEnvios.Tests.Infrastructure;

/// <summary>
/// La política acordada: un ticket que GLPI no tiene se rechaza, pero GLPI caído deja pasar.
/// Es la diferencia que hace que una caída de la mesa de ayuda no paralice a las filiales, y es
/// justo la que se rompería sin ruido si alguien "simplifica" el manejo de errores.
/// </summary>
public sealed class ValidadorTicketGlpiTests
{
    [Fact]
    public async Task DejaPasarUnTicketQueGlpiTiene()
    {
        var validador = Validador(Result<bool>.Success(true));

        var resultado = await validador.ValidarAsync("25000");

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task RechazaUnTicketQueGlpiNoTiene()
    {
        var validador = Validador(Result<bool>.Success(false));

        var resultado = await validador.ValidarAsync("99999999");

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.ErrorType);
        Assert.Contains("no existe en la mesa de ayuda", resultado.Error);
    }

    [Fact]
    public async Task ConGlpiCaidoDejaPasarEnVezDeBloquearLaOperacion()
    {
        var validador = Validador(Result<bool>.Failure("GLPI no responde.", ErrorType.ExternalService));

        var resultado = await validador.ValidarAsync("25000");

        Assert.True(resultado.IsSuccess);
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

    private static ValidadorTicketGlpi Validador(Result<bool> respuesta) =>
        new(new GlpiFalso(respuesta), NullLogger<ValidadorTicketGlpi>.Instance);

    private sealed class GlpiFalso(Result<bool> respuesta) : IGlpiClient
    {
        public Task<Result<bool>> ItemExistsAsync(string itemType, int id, CancellationToken ct = default) =>
            Task.FromResult(respuesta);
    }

    private sealed class GlpiQueFalla : IGlpiClient
    {
        public Task<Result<bool>> ItemExistsAsync(string itemType, int id, CancellationToken ct = default) =>
            throw new InvalidOperationException("No debió consultarse a GLPI con un ticket inválido.");
    }
}
