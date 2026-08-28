using FluentValidation;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Validators.Entities;

public sealed class UbicacionValidator : AbstractValidator<Ubicacion>
{
    public UbicacionValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.CodigoCentro)
            .NotEmpty()
            .MaximumLength(50);
    }
}
