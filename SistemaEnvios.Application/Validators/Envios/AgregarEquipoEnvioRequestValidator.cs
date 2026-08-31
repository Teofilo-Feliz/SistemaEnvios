using FluentValidation;
using SistemaEnvios.Application.DTOs.Envios;
namespace SistemaEnvios.Application.Validators.Envios;

public sealed class AgregarEquipoEnvioRequestValidator : AbstractValidator<AgregarEquipoEnvioRequest>
{
    public AgregarEquipoEnvioRequestValidator()
    {
        RuleFor(x => x.EnvioId).GreaterThan(0);
        RuleFor(x => x.EquipoId).GreaterThan(0);
        RuleFor(x => x.NumeroTicket).NotEmpty().MinimumLength(3).MaximumLength(50)
            .WithMessage("El número de ticket debe tener entre 3 y 50 dígitos.");
        RuleFor(x => x.NumeroTicket).Matches("^[0-9]+$")
            .WithMessage("El número de ticket solo puede contener caracteres numéricos.");
        RuleFor(x => x.Observaciones).NotEmpty().MaximumLength(2000);
    }
}
