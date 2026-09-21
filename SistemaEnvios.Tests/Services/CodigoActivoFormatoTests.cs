using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Validators.Equipos;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El código de activo de la ADR son hasta ocho dígitos. El formulario corta al teclear, pero la
/// regla vive aquí: es la que sostiene cualquier otro cliente de la API.
/// </summary>
public sealed class CodigoActivoFormatoTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("4545")]
    [InlineData("1")]
    public void AceptaHastaOchoDigitos(string codigo)
    {
        Assert.True(Crear(codigo).IsValid);
        Assert.True(Actualizar(codigo).IsValid);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("000000000000")]
    public void RechazaMasDeOcho(string codigo)
    {
        var resultado = Crear(codigo);

        Assert.False(resultado.IsValid);
        Assert.Contains("8 dígitos", resultado.Errors[0].ErrorMessage);
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("4545-A")]
    [InlineData("prueba")]
    public void RechazaLoQueNoSeanDigitos(string codigo)
    {
        Assert.False(Crear(codigo).IsValid);
        Assert.False(Actualizar(codigo).IsValid);
    }

    /// <summary>El código es opcional: no todos los equipos tienen etiqueta.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SinCodigoEsValido(string? codigo)
    {
        Assert.True(Crear(codigo).IsValid);
    }

    private static FluentValidation.Results.ValidationResult Crear(string? codigo) =>
        new CrearEquipoRequestValidator().Validate(new CrearEquipoRequest
        {
            CodigoActivo = codigo,
            TipoEquipoId = 1,
            UbicacionActualId = 1,
            Marca = "HP",
            Modelo = "Compaq",
        });

    private static FluentValidation.Results.ValidationResult Actualizar(string? codigo) =>
        new ActualizarEquipoRequestValidator().Validate(new ActualizarEquipoRequest
        {
            EquipoId = 1,
            CodigoActivo = codigo,
            TipoEquipoId = 1,
            UbicacionActualId = 1,
            Marca = "HP",
            Modelo = "Compaq",
        });
}
