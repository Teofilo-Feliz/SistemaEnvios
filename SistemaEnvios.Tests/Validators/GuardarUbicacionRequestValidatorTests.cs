using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Application.Validators.Ubicaciones;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Tests.Validators;

/// <summary>
/// FilialExternaId es el claim "affiliate" de AuthManager y es lo que ata la ubicación con sus
/// usuarios: el alcance por filial cruza ese campo contra el token. Una filial creada sin él no
/// la alcanza nadie, y el fallo no aparece al crearla sino el día que alguien de esa filial
/// intenta entrar y recibe "su filial no está asociada a ninguna ubicación".
/// </summary>
public sealed class GuardarUbicacionRequestValidatorTests
{
    private static GuardarUbicacionRequest Peticion(
        TipoUbicacionEnum tipo,
        int? filialExternaId,
        string nombre = "Santo Domingo",
        string codigo = "SDQ") =>
        new()
        {
            Nombre = nombre,
            CodigoCentro = codigo,
            Tipo = tipo,
            FilialExternaId = filialExternaId,
        };

    [Fact]
    public void UnaFilialSinIdDeAuthManagerSeRechaza()
    {
        var resultado = new GuardarUbicacionRequestValidator()
            .Validate(Peticion(TipoUbicacionEnum.Filial, null));

        Assert.False(resultado.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UnIdDeAuthManagerQueNoExisteSeRechaza(int externo)
    {
        var resultado = new GuardarUbicacionRequestValidator()
            .Validate(Peticion(TipoUbicacionEnum.Filial, externo));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void UnaFilialConSuIdSeAcepta()
    {
        var resultado = new GuardarUbicacionRequestValidator()
            .Validate(Peticion(TipoUbicacionEnum.Filial, 30));

        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors.Select(x => x.ErrorMessage)));
    }

    [Fact]
    public void TecnologiaConIdDeFilialSeRechaza()
    {
        // Tecnología no es de nadie: darle un id de filial la metería dentro del alcance de esa
        // filial y sus usuarios verían envíos que no les tocan.
        var resultado = new GuardarUbicacionRequestValidator()
            .Validate(Peticion(TipoUbicacionEnum.Tecnologia, 30));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void TecnologiaSinIdSeAcepta()
    {
        var resultado = new GuardarUbicacionRequestValidator()
            .Validate(Peticion(TipoUbicacionEnum.Tecnologia, null, "Tecnología", "TEC"));

        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors.Select(x => x.ErrorMessage)));
    }
}
