using FluentValidation;
using PlayOffsApi.Enum;
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

		RuleFor(c => c.NumberOfPlayers)
			.NotEmpty()
			.WithMessage(Resource.ChampionshipValidatorNumberOfPlayers);
		RuleFor(championship => championship.SportsId)
			.Must((championship, sportsId) =>
			{
				var sports = (Sports)sportsId;
				var format = (Format)championship.Format;
				return !(sports == Sports.Volleyball &&
						(format == Format.Knockout ||
						format == Format.GroupStage) &&
						(championship.DoubleMatchEliminations ||
						championship.FinalDoubleMatch));
			}).WithMessage("Campeonato de vôlei com eliminatórias não pode ter partidas duplas.");
		RuleFor(championship => championship.Format)
			.NotEqual(Format.LeagueSystem)
            .When(championship => championship.DoubleMatchGroupStage || championship.DoubleMatchEliminations || championship.FinalDoubleMatch)
            .WithMessage("Campeonato de pontos corridos não apresenta eliminatórias");
		RuleFor(championship => championship.Format)
			.Equal(Format.LeagueSystem)
            .When(championship => championship.DoubleStartLeagueSystem)
            .WithMessage("Partida duplas para pontos corridos disponível apenas para esse formato");
		RuleFor(championship => championship.Format)
			.Equal(Format.GroupStage)
            .When(championship => championship.DoubleMatchGroupStage)
            .WithMessage("Partida duplas para fase de grupos disponível apenas para esse formato");
	}

	private static bool IsPowerOfTwo(int x) => (x is not 0 && (x & (x - 1)) is 0) || (x >= 4 && x % 2 == 0 && x <= 20);
}