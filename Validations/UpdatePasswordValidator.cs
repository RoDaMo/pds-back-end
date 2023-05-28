using System.Text.RegularExpressions;
using FluentValidation;
using PlayOffsApi.DTO;

namespace PlayOffsApi.Validations;

public partial class UpdatePasswordValidator : AbstractValidator<UpdatePasswordDTO>
{
    public UpdatePasswordValidator()
    {
        RuleFor(rule => rule.NewPassword)
                .NotEmpty()
                .WithMessage("Insira sua nova senha.");

        RuleFor(rule => rule.NewPassword)
            .Matches(PasswordRegex())
            .WithMessage("Nova Senha inválida.");
        RuleFor(rule => rule.NewPassword)
            .NotEqual(rule => rule.CurrentPassword)
            .WithMessage("Nova Senha não pode ser igual à atual.");

        RuleFor(rule => rule.CurrentPassword)
                .NotEmpty()
                .WithMessage("Insira sua senha atual.");
    }

    [GeneratedRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)([-\\w]{4,100})$")]
    private static partial Regex PasswordRegex();
}
