using System.Text.RegularExpressions;
using FluentValidation;
using PlayOffsApi.Models;

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
                .WithMessage("Informe seu nome de usuário, ele será utilizado para permitir que outros usuários o identifiquem sem revelar seu nome");

            RuleFor(rule => rule.Username)
                .Matches(UsernameRegex())
                .WithMessage(@"Nome de usuário inválido, seu nome de usuário deve conter apenas letras maíusculas, minúsculas, números e opcionalmente ""-"",""_"". Assim como deve possuir pelo menos 4 caracteres e no máximo 100.");
        });
        
        RuleSet("IdentificadorEmail", () =>
        {
            RuleFor(rule => rule.Email)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Endereço de email inválido");
        });
        
        RuleSet("Dados", () =>
        {
            RuleFor(rule => rule.Name.ToLower())
                .NotEmpty()
                .WithMessage("Informe o seu nome")
                .NotEqual("null")
                .WithMessage("Informe o seu nome")
                .Length(4, 200)
                .WithMessage("Nome deve possuir pelo menos 4 caracteres e no máximo 200."); // biggest name in the world had over 600 characters
            
            RuleFor(rule => rule.Birthday)
                .NotEmpty()
                .WithMessage("Informe sua data de nascimento");

            RuleFor(rule => rule.Birthday)
                .LessThan(DateTime.Now.AddYears(-13))
                .WithMessage("É necessário possuir pelo menos 13 anos de idade para se cadastrar");


            RuleFor(rule => rule.Password)
                .NotEmpty()
                .WithMessage("Insira sua senha");

            RuleFor(rule => rule.Password)
                .Matches(PasswordRegex())
                .WithMessage("Senha inválida");
        });


         RuleSet("Password", () =>
        {
            RuleFor(rule => rule.Password)
                .NotEmpty()
                .WithMessage("Insira sua senha");

            RuleFor(rule => rule.Password)
                .Matches(PasswordRegex())
                .WithMessage("Senha inválida");
        });

        RuleSet("Bio", () =>
        {
            RuleFor(rule => rule.Bio)
                .Length(0, 300) 
                .WithMessage("Campo Bio pode ter no máximo 300 caracteres.");
        }); 
        
        RuleSet("Name", () => RuleFor(rule => rule.Name)
            .NotEmpty()
            .WithMessage("Informe o seu nome")
            .NotEqual("null")
            .WithMessage("Informe o seu nome")
            .Length(4, 200)
            .WithMessage("Nome deve possuir pelo menos 4 caracteres e no máximo 200."));

        RuleSet("UpdatePassword", () =>
        {
            RuleFor(rule => rule.Password)
                .NotEmpty()
                .WithMessage("Insira sua senha");
        });
        
        RuleSet("Cpf", () =>
        {
            RuleFor(t => t.Cpf)
                .NotEmpty()
                .WithMessage("Campo CPF não pode ser vazio.");
            RuleFor(t => t.Cpf)
                .Length(11, 11)
                .WithMessage("Campo CPF deve ter 11 caracteres.");
            RuleFor(t => t.Cpf)
                .Matches(@"^\d+$")
                .WithMessage("Campos CPF deve conter apenas números");
        });
    }
    
    // used to improve validation performance,
    // as it is generated at compile time
    [GeneratedRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)([-\\w]{4,100})$")]
    private static partial Regex PasswordRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]*$")]
    private static partial Regex UsernameRegex();
}