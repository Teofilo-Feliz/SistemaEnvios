using FluentValidation;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
namespace SistemaEnvios.Application.Validators.Envios;

public sealed class AgregarEquipoEnvioRequestValidator : AbstractValidator<AgregarEquipoEnvioRequest>
{
    public AgregarEquipoEnvioRequestValidator()
    {
        RuleFor(x => x.EnvioId).GreaterThan(0);
        RuleFor(x => x.EquipoId).GreaterThan(0);
        RuleFor(x => x.NumeroTicket)
            .Must(NumeroTicket.TieneFormato).WithMessage(NumeroTicket.MensajeFormato)
            .Must(NumeroTicket.EsMayorQueCero).WithMessage(NumeroTicket.MensajeMayorQueCero);
        RuleFor(x => x.Observaciones).NotEmpty().MaximumLength(2000);
    }
}
