using FluentValidation;
using PlayOffsApi.Models;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Controllers.Validations;

public class ChampionshipValidator : AbstractValidator<Championship>
{
	public ChampionshipValidator()
	{
		RuleFor(c => c.Name)
			.NotEmpty()
			.WithMessage(Resource.NameNotNull);
		RuleFor(c => c.Name)
			 .Length(4, 50)
			 .WithMessage(Resource.NameCharRange);

		RuleFor(c => c.Prize)
			.NotEmpty()
			.WithMessage(Resource.PrizeNotNull);
		RuleFor(c => c.Prize)
			 .Length(3, 20)
			 .WithMessage(Resource.PrizeCharRange);

		RuleFor(c => c.InitialDate)
			.NotEmpty()
			.WithMessage(Resource.InitialDateNotNull);
		RuleFor(c => c.InitialDate)
			.GreaterThanOrEqualTo(c => DateTime.UtcNow.Date)
			.WithMessage(Resource.InitialDateRange);
		RuleFor(c => c.InitialDate)
			.LessThanOrEqualTo(c => c.FinalDate)
			.WithMessage(Resource.FinalDateRange);

		RuleFor(c => c.FinalDate)
			 .NotEmpty()
			 .WithMessage(Resource.FinalDateNotNull);

		RuleFor(c => c.SportsId)
			.NotEmpty()
			.WithMessage("Campo Esporte não pode ser vazio.");
		RuleFor(c => c.SportsId)
			.Must(c => c.Equals(1) || c.Equals(2))
			.WithMessage("Campo Esporte deve ser preenchido com Vôlei ou futebol.");
	}
}