using System.Text.RegularExpressions;
using FluentValidation;
using PlayOffsApi.DTO;
using Resource = PlayOffsApi.Resources.Validations.UpdatePassword.UpdatePassword;

namespace PlayOffsApi.Validations;

public partial class UpdatePasswordValidator : AbstractValidator<UpdatePasswordDTO>
{
    public UpdatePasswordValidator()
    {
        RuleFor(rule => rule.NewPassword)
            .NotEmpty()
            .WithMessage(Resource.NewPasswordNull);

        RuleFor(rule => rule.NewPassword)
            .Matches(PasswordRegex())
            .WithMessage(Resource.InvalidNewPassword);
        RuleFor(rule => rule.NewPassword)
            .NotEqual(rule => rule.CurrentPassword)
            .WithMessage(Resource.NewPasswordNotEqualOld);

        RuleFor(rule => rule.CurrentPassword)
            .NotEmpty()
            .WithMessage(Resource.CurrentPasswordNull);
    }

    [GeneratedRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)([-\\w]{4,100})$")]
    private static partial Regex PasswordRegex();
}
