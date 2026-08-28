using FluentValidation;
using SistemaEnvios.Application.DTOs.Transportes;
namespace SistemaEnvios.Application.Validators.Transportes;

public sealed class CrearTransporteRequestValidator : AbstractValidator<CrearTransporteRequest>
{
    public CrearTransporteRequestValidator()
    {
        RuleFor(x => x.EnvioId).GreaterThan(0);
        RuleFor(x => x.Tipo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NombreChofer).MaximumLength(150);
        RuleFor(x => x.Placa).MaximumLength(20);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
