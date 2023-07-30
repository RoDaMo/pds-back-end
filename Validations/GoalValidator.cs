using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public class GoalValidator : AbstractValidator<Goal>
{
    public GoalValidator()
    {
        RuleSet("ValidationVolleyBall", () => 
        {
			RuleFor(g => g.TeamId)
				.NotEmpty()
				.WithMessage("Campo Time não pode ser vazio.");
            RuleFor(g => g.MatchId)
				.NotEmpty()
				.WithMessage("Campo Partida não pode ser vazio.");
            RuleFor(g => g.PlayerId)
                .NotEmpty()
                .WithMessage("O Campo Jogador não pode ser vazio.")
                .When(g => g.PlayerTempId == Guid.Empty);
            RuleFor(g => g.PlayerTempId)
                .NotEmpty()
                .WithMessage("O Campo Jogador não pode ser vazio.")
                .When(g => g.PlayerId == Guid.Empty);
            RuleFor(g => g.Set)
                .InclusiveBetween(1, 5)
                .WithMessage("Set inválido.")
                .When(g => g.PlayerId == Guid.Empty);
		});

        RuleSet("ValidationSoccer", () => 
        {
			RuleFor(g => g.TeamId)
				.NotEmpty()
				.WithMessage("Campo Time não pode ser vazio.");
            RuleFor(g => g.MatchId)
				.NotEmpty()
				.WithMessage("Campo Partida não pode ser vazio.");
            RuleFor(g => g.PlayerId)
                .NotEmpty()
                .WithMessage("O Campo Jogador não pode ser vazio.")
                .When(g => g.PlayerTempId == Guid.Empty);
            RuleFor(g => g.PlayerTempId)
                .NotEmpty()
                .WithMessage("O Campo Jogador não pode ser vazio.")
                .When(g => g.PlayerId == Guid.Empty);
            RuleFor(g => g.Set)
                .Must(set => set == 0)
                .WithMessage("Futebol não possui sets.");
		});
    }
    
}