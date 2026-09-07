using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Validators.Envios;

namespace SistemaEnvios.Tests.Validators;

/// <summary>
/// El ticket es el identificador de un caso en la mesa de ayuda, y ahí no existe el cero. Un
/// "0" o un "000" son numéricos y del largo permitido, así que pasaban todos los filtros de
/// formato y solo se caían más tarde —o no se caían— según por qué camino entraran.
/// </summary>
public sealed class NumeroTicketMayorQueCeroTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("000")]
    [InlineData("0000000")]
    public void AgregarRechazaUnTicketQueValeCero(string ticket)
    {
        var resultado = new AgregarEquipoEnvioRequestValidator().Validate(new AgregarEquipoEnvioRequest
        {
            EnvioId = 1,
            EquipoId = 1,
            NumeroTicket = ticket,
            Observaciones = "Prueba",
        });

        Assert.False(resultado.IsValid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("000")]
    [InlineData("0000000")]
    public void ActualizarRechazaUnTicketQueValeCero(string ticket)
    {
        var resultado = new ActualizarEnvioEquipoRequestValidator().Validate(new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = 1,
            NumeroTicket = ticket,
            Observaciones = "Prueba",
        });

        Assert.False(resultado.IsValid);
    }

    [Theory]
    [InlineData("001")]
    [InlineData("25000")]
    [InlineData("007")]
    public void ActualizarAceptaUnTicketRealAunqueEmpieceEnCero(string ticket)
    {
        // Los ceros a la izquierda no son el problema: el problema es que el número sea cero.
        var resultado = new ActualizarEnvioEquipoRequestValidator().Validate(new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = 1,
            NumeroTicket = ticket,
            Observaciones = "Prueba",
        });

        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors.Select(x => x.ErrorMessage)));
    }

    [Fact]
    public void ActualizarExigeElMismoLargoMinimoQueAgregar()
    {
        // Agregar pide 3 dígitos y Actualizar no pedía ninguno: el mismo ticket se rechazaba al
        // crearlo y se aceptaba al editarlo.
        var resultado = new ActualizarEnvioEquipoRequestValidator().Validate(new ActualizarEnvioEquipoRequest
        {
            EnvioEquipoId = 1,
            NumeroTicket = "12",
            Observaciones = "Prueba",
        });

        Assert.False(resultado.IsValid);
    }
}
