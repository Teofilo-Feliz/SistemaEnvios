using FluentValidation;
using SistemaEnvios.Application.DTOs.Recepciones;
namespace SistemaEnvios.Application.Validators.Recepciones;

public sealed class VerificarEquipoRequestValidator : AbstractValidator<VerificarEquipoRequest>
{
    public VerificarEquipoRequestValidator()
    {
        RuleFor(x => x.RecepcionId).GreaterThan(0);
        RuleFor(x => x.EnvioEquipoId).GreaterThan(0);
        RuleFor(x => x.Estado).IsInEnum().NotEqual(Domain.Enums.EstadoRecepcionEquipoEnum.Pendiente);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
