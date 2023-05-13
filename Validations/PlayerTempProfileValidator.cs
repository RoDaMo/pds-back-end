using FluentValidation;
using PlayOffsApi.Models;

namespace PlayOffsApi.Validations;

public class PlayerTempProfileValidator : AbstractValidator<PlayerTempProfile>
{
    public PlayerTempProfileValidator()
	{
		
		RuleSet("ValidationSoccer", () => {
			RuleFor(p => p.Name)
				.NotEmpty()
				.WithMessage("Campo Nome não pode ser vazio.");
			RuleFor(p => p.Name)
				.Length(4, 50)
				.WithMessage("Campo Nome deve ter entre 4 e 50 caracteres.");

			RuleFor(p => p.ArtisticName)
				.NotEmpty()
				.WithMessage("Campo Nome Artístico não pode ser vazio.");
			RuleFor(p => p.ArtisticName)
				.Length(4, 50)
				.WithMessage("Campo Nome Artístico deve ter entre 4 e 50 caracteres.");
			
			RuleFor(p => p.Email)
				.NotEmpty()
				.WithMessage("Campo Email não pode ser vazio.");

			RuleFor(p => p.Number)
				.NotEmpty()
				.WithMessage("Campo Número não pode ser vazio.");
			RuleFor(p => p.Number)
				.InclusiveBetween(1, 99)
				.WithMessage("O Campo Número deve estar entre 1 e 99");

			RuleFor(p => p.TeamsId)
				.NotEmpty()
				.WithMessage("Campo Time não pode ser vazio.");
		});

		RuleSet("ValidationVolleyBall", () => {
			RuleFor(p => p.Name)
				.NotEmpty()
				.WithMessage("Campo Nome não pode ser vazio.");
			RuleFor(p => p.Name)
				.Length(4, 50)
				.WithMessage("Campo Nome deve ter entre 4 e 50 caracteres.");

			RuleFor(p => p.ArtisticName)
				.NotEmpty()
				.WithMessage("Campo Nome Artístico não pode ser vazio.");
			RuleFor(p => p.ArtisticName)
				.Length(4, 50)
				.WithMessage("Campo Nome Artístico deve ter entre 4 e 50 caracteres.");
			
			RuleFor(p => p.Email)
				.NotEmpty()
				.WithMessage("Campo Email não pode ser vazio.");

			RuleFor(p => p.Number)
				.NotEmpty()
				.WithMessage("Campo Número não pode ser vazio.");
			RuleFor(p => p.Number)
				.InclusiveBetween(1, 99)
				.WithMessage("O Campo Número deve estar entre 1 e 20");

			RuleFor(p => p.TeamsId)
				.NotEmpty()
				.WithMessage("Campo Time não pode ser vazio.");
		});
		
	}
}
