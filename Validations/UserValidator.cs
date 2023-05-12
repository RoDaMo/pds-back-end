﻿using System.Text.RegularExpressions;
using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public partial class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(rule => rule.Name.ToLower())
            .NotEmpty()
            .Length(4, 200) // biggest name in the world had over 600 characters
            .NotEqual("null")
            .WithMessage("Informe o seu nome");

        RuleFor(rule => rule.Username.ToLower())
            .NotEmpty()
            .NotEqual("null")
            .WithMessage("Informe seu nome de usuário, ele será utilizado para permitir que outros usuários o identifiquem sem revelar seu nome");

        RuleFor(rule => rule.Username)
            .Matches(PasswordRegex())
            .WithMessage(@"Nome de usuário inválido, seu nome de usuário deve conter apenas letras maíusculas, minúsculas, números e opcionalmente ""-"",""_"". Assim como deve possuir pelo menos 4 caracteres e no máximo 100.");

        RuleFor(rule => rule.Birthday)
            .NotEmpty()
            .WithMessage("Informe sua data de nascimento");

        RuleFor(rule => rule.Birthday)
            .LessThan(DateTime.Now.AddYears(-13))
            .WithMessage("É necessário possuir pelo menos 13 anos de idade para se cadastrar");

        RuleFor(rule => rule.Email)
            .EmailAddress()
            .WithMessage("Endereço de email inválido");

        RuleFor(rule => rule.Password)
            .NotEmpty()
            .WithMessage("Insira sua senha");

        RuleFor(rule => rule.Password)
            .Matches(PasswordRegex())
            .WithMessage("Senha inválida");
    }
    
    // used to improve validation performance,
    // as it is generated at compile time
    [GeneratedRegex("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)([-\\w]{4,100})$")]
    private static partial Regex PasswordRegex();
}