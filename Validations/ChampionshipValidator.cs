using FluentValidation;
using PlayOffsApi.Models;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Validations;

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
			.WithMessage(Resource.ChampionshipValidatorSportNotNull);
		
		RuleFor(c => c.SportsId)
			.Must(c => c.Equals(Sports.Football) || c.Equals(Sports.Volleyball))
			.WithMessage(Resource.ChampionshipValidatorFootballOrVolley);

		RuleFor(c => c.TeamQuantity)
			.Must(IsPowerOfTwo)
			.WithMessage(Resource.ChampionshipValidatorInvalidQuantity);

		RuleFor(c => c.TeamQuantity)
			.LessThanOrEqualTo(128)
			.WithMessage(Resource.ChampionshipValidatorMaximumQuantityExceeded);

		RuleFor(c => c.TeamQuantity)
			.GreaterThanOrEqualTo(2)
			.WithMessage(Resource.ChampionshipValidatorAtleastTwo);

		RuleFor(c => c.Description)
			.Length(10, 10000)
			.WithMessage(Resource.ChampionshipValidatorAtleast10);

		RuleFor(c => c.Nation)
			.NotEmpty()
			.WithMessage(Resource.ChampionshipValidatorNationNotNull);

		RuleFor(c => c.State)
			.NotEmpty()
			.WithMessage(Resource.ChampionshipValidatorStateNotNull);
		
		RuleFor(c => c.City)
			.NotEmpty()
			.WithMessage(Resource.ChampionshipValidatorCityNotNull);

		RuleFor(c => c.Neighborhood)
			.NotEmpty()
			.WithMessage(Resource.ChampionshipValidatorNeighborhoodNotNull);

		RuleFor(c => c.NumberOfPlayers)
			.NotEmpty()
			.WithMessage(Resource.ChampionshipValidatorNumberOfPlayers);

	}

	private static bool IsPowerOfTwo(int x) => (x is not 0 && (x & (x - 1)) is 0) || (x >= 4 && x % 2 == 0 && x <= 20);
}