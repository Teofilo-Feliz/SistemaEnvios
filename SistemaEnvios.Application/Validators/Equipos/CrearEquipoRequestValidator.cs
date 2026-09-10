using FluentValidation;
using SistemaEnvios.Application.DTOs.Equipos;

namespace SistemaEnvios.Application.Validators.Equipos;

public sealed class CrearEquipoRequestValidator : AbstractValidator<CrearEquipoRequest>
{
    public CrearEquipoRequestValidator()
    {
        RuleFor(x => x.TipoEquipoId).GreaterThan(0);
        RuleFor(x => x.UbicacionActualId).GreaterThan(0);
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(100);
        // Solo dígitos: el código de activo es numérico y una letra ahí es un error de tecleo.
        // El formulario ya lo filtra al escribir; esto lo sostiene para cualquier otro cliente.
        RuleFor(x => x.CodigoActivo)
            .Matches("^[0-9]+$").WithMessage("El código de activo solo admite dígitos.")
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.CodigoActivo));
        RuleFor(x => x.NumeroSerie).MaximumLength(100);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
