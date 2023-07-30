using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public class PenaltyValidator :  AbstractValidator<Penalty>
{
    public PenaltyValidator()
    {
        RuleFor(p => p.TeamId)
				.NotEmpty()
				.WithMessage("Campo Time não pode ser vazio.");
            RuleFor(p => p.MatchId)
				.NotEmpty()
				.WithMessage("Campo Partida não pode ser vazio.");
            RuleFor(p => p.PlayerId)
                .NotEmpty()
                .WithMessage("O Campo Jogador não pode ser vazio.")
                .When(g => g.PlayerTempId == Guid.Empty);
            RuleFor(p => p.PlayerTempId)
                .NotEmpty()
                .WithMessage("O Campo Jogador não pode ser vazio.")
                .When(g => g.PlayerId == Guid.Empty);
    }
    
}