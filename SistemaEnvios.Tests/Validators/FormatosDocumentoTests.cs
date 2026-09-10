using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Tests.Validators;

/// <summary>
/// El documento del responsable de un transporte privado se valida en tres capas —petición,
/// servicio y CHECK de la tabla—, y las tres leen esta regla. Lo que se prueba aquí es que la
/// cédula no admita letras, que el pasaporte sí, y que ninguna de las dos deje pasar basura.
/// </summary>
public sealed class FormatosDocumentoTests
{
    [Theory]
    [InlineData("40209815378")]
    [InlineData("00112345678")]
    [InlineData("402-0981537-8")]   // Con guiones: se limpian al normalizar.
    [InlineData(" 40209815378 ")]   // Con espacios de sobra.
    public void CedulaAceptaOnceDigitos(string valor) =>
        Assert.True(FormatosDocumento.EsValido(TipoDocumentoEnum.Cedula, valor));

    [Theory]
    [InlineData("4020981537A")]     // Una letra: es lo que se pidió impedir.
    [InlineData("RD1234567")]       // Un pasaporte no vale como cédula.
    [InlineData("4020981537")]      // Diez dígitos.
    [InlineData("402098153789")]    // Doce dígitos.
    [InlineData("")]
    [InlineData("   ")]
    public void CedulaRechazaLetrasYLargosMalos(string valor) =>
        Assert.False(FormatosDocumento.EsValido(TipoDocumentoEnum.Cedula, valor));

    [Theory]
    [InlineData("RD1234567")]
    [InlineData("rd1234567")]       // Minúsculas: se suben al normalizar.
    [InlineData("123456")]          // Solo dígitos también es un pasaporte válido.
    [InlineData("ABCDEFGHIJ12345")] // Quince, el máximo.
    public void PasaporteAceptaLetrasYDigitos(string valor) =>
        Assert.True(FormatosDocumento.EsValido(TipoDocumentoEnum.Pasaporte, valor));

    [Theory]
    [InlineData("RD123")]           // Cinco: por debajo del mínimo.
    [InlineData("ABCDEFGHIJ123456")]// Dieciséis: por encima del máximo.
    [InlineData("RD-123456")]       // El guion no es parte del número.
    [InlineData("RD/123456")]
    [InlineData("")]
    public void PasaporteRechazaLargosMalosYSimbolos(string valor) =>
        Assert.False(FormatosDocumento.EsValido(TipoDocumentoEnum.Pasaporte, valor));

    [Fact]
    public void LaCedulaSeGuardaSoloConDigitos() =>
        Assert.Equal("40209815378", FormatosDocumento.Normalizar(TipoDocumentoEnum.Cedula, "402-0981537-8"));

    /// <summary>
    /// La colación de la base ignora mayúsculas, así que sin subirlas el mismo pasaporte entraría
    /// dos veces escrito de dos formas y parecerían responsables distintos.
    /// </summary>
    [Fact]
    public void ElPasaporteSeGuardaEnMayusculas() =>
        Assert.Equal("RD1234567", FormatosDocumento.Normalizar(TipoDocumentoEnum.Pasaporte, " rd123 4567 "));
}
