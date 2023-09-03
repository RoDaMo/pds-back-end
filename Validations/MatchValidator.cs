using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public class MatchValidator : AbstractValidator<Match>
{
    public MatchValidator()
    {
        RuleFor(m => m.Date)
            .GreaterThanOrEqualTo(c => DateTime.UtcNow.Date)
            .WithMessage("A data não pode ser anterior à data de hoje.");
        RuleFor(m => m.Arbitrator)
            .Length(4, 200)
            .WithMessage("Árbitro deve possuir pelo menos 4 caracteres e no máximo 200.");
    }

}