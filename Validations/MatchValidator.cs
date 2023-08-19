using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public class MatchValidator : AbstractValidator<Match>
{
    public MatchValidator()
    {
        RuleFor(m => m.Date)
            .Must(date => date >= DateTime.UtcNow)
            .WithMessage("A data não pode ser anterior à data de hoje.")
            .When(date => date is not null);
         RuleFor(m => m.Arbitrator)
            .Length(4, 200)
            .WithMessage("Árbitro deve possuir pelo menos 4 caracteres e no máximo 200.")
            .When(arbitrator => arbitrator is not null);
    }

}