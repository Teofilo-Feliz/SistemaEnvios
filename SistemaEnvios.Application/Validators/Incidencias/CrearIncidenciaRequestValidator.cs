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
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x).Must(x => x.EnvioEquipoId.HasValue || x.TransporteId.HasValue).WithMessage("La incidencia debe asociarse a un equipo o transporte.");
    }
}
