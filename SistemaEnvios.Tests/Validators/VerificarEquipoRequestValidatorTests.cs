using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Tests.Validators;

public sealed class VerificarEquipoRequestValidatorTests
{
    [Fact]
    public void ConIncidenciaSinObservacion_EsInvalido()
    {
        var request = new VerificarEquipoRequest
        {
            RecepcionId = 1,
            EnvioEquipoId = 1,
            Estado = EstadoRecepcionEquipoEnum.VerificadoConIncidencia
        };

        var resultado = new VerificarEquipoRequestValidator().Validate(request);

        Assert.False(resultado.IsValid);
    }
}
