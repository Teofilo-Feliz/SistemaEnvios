using FluentValidation;
using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.Validators.Ubicaciones;

public sealed class GuardarUbicacionRequestValidator : AbstractValidator<GuardarUbicacionRequest>
{
    public GuardarUbicacionRequestValidator()
    {
        RuleFor(x => x.UbicacionId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CodigoCentro).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Tipo).IsInEnum();

        // Una filial sin su id de AuthManager no la alcanza ningún usuario: el alcance por filial
        // cruza este campo contra el claim "affiliate" del token. Se exige al crearla y no
        // después, porque "después" en la práctica significa nunca y el fallo aparece el día que
        // alguien de esa filial intenta entrar.
        RuleFor(x => x.FilialExternaId)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.Tipo == TipoUbicacionEnum.Filial)
            .WithMessage("Una filial necesita el id que tiene en AuthManager para que sus usuarios la alcancen.");

        // Al revés también: Tecnología no es de nadie, y darle un id de filial la metería dentro
        // del alcance de esa filial.
        RuleFor(x => x.FilialExternaId)
            .Null()
            .When(x => x.Tipo != TipoUbicacionEnum.Filial)
            .WithMessage("Solo una filial lleva id de AuthManager.");
    }
}
