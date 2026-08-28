using FluentValidation;
using SistemaEnvios.Application.DTOs.Incidencias;
namespace SistemaEnvios.Application.Validators.Incidencias;

public sealed class CrearIncidenciaRequestValidator : AbstractValidator<CrearIncidenciaRequest>
{
    public CrearIncidenciaRequestValidator()
    {
        RuleFor(x => x.EnvioId).GreaterThan(0);
        RuleFor(x => x.EnvioEquipoId).GreaterThan(0).When(x => x.EnvioEquipoId.HasValue);
        RuleFor(x => x.TransporteId).GreaterThan(0).When(x => x.TransporteId.HasValue);
        RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => !(x.EnvioEquipoId.HasValue && x.TransporteId.HasValue))
            .WithMessage("La incidencia puede asociarse al envío, a un equipo o al transporte, pero no a más de uno.");
    }
}
