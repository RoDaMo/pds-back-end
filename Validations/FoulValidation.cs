using FluentValidation;

namespace PlayOffsApi.Validations;

public class FoulValidation : AbstractValidator<Foul>
{
    public FoulValidation()
    {
        RuleFor(f => f.MatchId)
			.NotEmpty()
			.WithMessage("Campo Partida não pode ser vazio.");
        
        RuleFor(f => f.PlayerId)
            .NotEmpty()
            .WithMessage("O campo Jogador não pode ser vazio.")
            .When(f => f.PlayerTempId == Guid.Empty);
        
        RuleFor(f => f.PlayerTempId)
            .NotEmpty()
            .WithMessage("O campo Jogador não pode ser vazio.")
            .When(f => f.PlayerId == Guid.Empty);
        
         RuleFor(f => f.Minutes)
            .NotEmpty()
            .WithMessage("O campo Minutos não pode ser vazio"); 
    }
}