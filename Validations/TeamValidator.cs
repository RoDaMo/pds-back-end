using FluentValidation;
using PlayOffsApi.DTO;

namespace PlayOffsApi.Validations;

public class TeamValidator : AbstractValidator<TeamDTO>
{
    public TeamValidator()
	{
		RuleFor(t => t.Name)
			.NotEmpty()
			.WithMessage("Campo Nome não pode ser vazio.");
		RuleFor(t => t.Name)
			 .Length(4, 50)
			 .WithMessage("Campo Nome deve ter entre 4 e 50 caracteres.");

		RuleFor(t => t.Emblem)
			.NotEmpty()
			.WithMessage("Campo Emblema não pode ser vazio.");

		RuleFor(t => t.UniformHome)
			.NotEmpty()
			.WithMessage("Campo Uniforme de Casa não pode ser vazio.");

		RuleFor(t => t.UniformAway)
			.NotEmpty()
			.WithMessage("Campo Uniforme de Fora de Casa não pode ser vazio.");

		RuleFor(t => t.SportsId)
			.NotEmpty()
			.WithMessage("Campo Esporte não pode ser vazio.");
		RuleFor(t => t.SportsId)
			.Must(t => t.Equals(1) || t.Equals(2))
			.WithMessage("Campo Esporte deve ser preenchido com vôlei ou futebol.");
	}
}
