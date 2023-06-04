using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public class PlayerValidator : AbstractValidator<User>
{
    public PlayerValidator()
	{
			RuleFor(p => p.ArtisticName)
				.NotEmpty()
				.WithMessage("Campo Nome Artístico não pode ser vazio.");
			RuleFor(p => p.ArtisticName)
				.Length(4, 50)
				.WithMessage("Campo Nome Artístico deve ter entre 4 e 50 caracteres.");

			RuleFor(p => p.Number)
				.NotEmpty()
				.WithMessage("Campo Número não pode ser vazio.");
			RuleFor(p => p.Number)
				.InclusiveBetween(1, 99)
				.WithMessage("O Campo Número deve estar entre 1 e 99");

			RuleFor(p => p.PlayerTeamId)
				.NotEmpty()
				.WithMessage("Campo Time não pode ser vazio.");
			
			RuleFor(p => p.PlayerPosition)
				.NotEmpty()
				.WithMessage("Campo Posição não pode ser vazio.");
    }
}
