using System.Text.RegularExpressions;
using FluentValidation;
using PlayOffsApi.Models;
using Resource = PlayOffsApi.Resources.Validations.User.User;

namespace PlayOffsApi.Validations;

public partial class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleSet("IdentificadorUsername", () =>
        {
            RuleFor(rule => rule.Username.ToLower())
                .NotEmpty()
                .NotEqual("null")
                .WithMessage(Resource.UserValidatorUsernameNotNull);

            RuleFor(rule => rule.Username)
                .Matches(UsernameRegex())
                .WithMessage(Resource.UserValidatorInvalidUsername);
        });
        
        RuleSet("IdentificadorEmail", () =>
        {
            RuleFor(rule => rule.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage(Resource.UserValidatorInvalidEmail);
        });
        
        RuleSet("Dados", () =>
        {
            RuleFor(rule => rule.Name.ToLower())
                .NotEmpty()
                .WithMessage(Resource.UserValidatorNameNotNull)
                .NotEqual("null")
                .WithMessage(Resource.UserValidatorNameNotNull)
                .Length(4, 200)
                .WithMessage(Resource.UserValidatorInvalidNameLength); // biggest name in the world had over 600 characters
            
            RuleFor(rule => rule.Birthday)
                .NotEmpty()
                .WithMessage(Resource.UserValidatorDateBirthNotNull);

            RuleFor(rule => rule.Birthday)
                .LessThan(DateTime.Now.AddYears(-13))
                .WithMessage(Resource.UserValidatorAtleast13);


            RuleFor(rule => rule.Password)
                .NotEmpty()
                .WithMessage(Resource.UserValidatorPasswordNotNull);

            RuleFor(rule => rule.Password)
                .Matches(PasswordRegex())
                .WithMessage(Resource.UserValidatorInvalidPassword);
        });


         RuleSet("Password", () =>
        {
            RuleFor(rule => rule.Password)
                .NotEmpty()
                .WithMessage(Resource.UserValidatorPasswordNotNull);

            RuleFor(rule => rule.Password)
                .Matches(PasswordRegex())
                .WithMessage(Resource.UserValidatorInvalidPassword);
        });

        RuleSet("Bio", () =>
        {
            RuleFor(rule => rule.Bio)
                .Length(0, 300) 
                .WithMessage(Resource.UserValidatorBioInvalidLength);
        }); 
        
        RuleSet("Name", () => RuleFor(rule => rule.Name)
            .NotEmpty()
            .WithMessage(Resource.UserValidatorNameNotNull)
            .NotEqual("null")
            .WithMessage(Resource.UserValidatorNameNotNull)
            .Length(4, 200)
            .WithMessage(Resource.UserValidatorInvalidNameLength));

        RuleSet("UpdatePassword", () =>
        {
            RuleFor(rule => rule.Password)
                .NotEmpty()
                .WithMessage(Resource.UserValidatorPasswordNotNull);
        });
        
        RuleSet("Cpf", () =>
        {
            RuleFor(t => t.Cpf)
                .NotEmpty()
                .WithMessage(Resource.UserValidatorCpfNotNull);
            RuleFor(t => t.Cpf)
                .Length(11, 11)
                .WithMessage(Resource.UserValidatorInvalidLength);
            RuleFor(t => t.Cpf)
                .Matches(@"^\d+$")
                .WithMessage(Resource.UserValidatorOnlyNumbers);
        });
        
        RuleSet("Cnpj", () =>
        {
            RuleFor(t => t.Cnpj)
                .NotEmpty()
                .WithMessage("CNPJ não pode estar vazia.");
            RuleFor(t => t.Cpf)
                .Length(11, 11)
                .WithMessage("CNPJ inválida.");
            RuleFor(t => t.Cpf)
                .Matches(@"^\d+$")
                .WithMessage("Insira somente números.");
        });
    }
    
    // used to improve validation performance,
    // as it is generated at compile time
    [GeneratedRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)([-\\w]{4,100})$")]
    private static partial Regex PasswordRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]*$")]
    private static partial Regex UsernameRegex();
}