using FluentValidation;
using PlayOffsApi.DTO;
using Resource = PlayOffsApi.Resources.Validations.Team.Team;

namespace PlayOffsApi.Validations;

public class TeamValidator : AbstractValidator<TeamDTO>
{
    public TeamValidator()
	{
		RuleFor(t => t.Name)
			.NotEmpty()
			.WithMessage(Resource.NameNotNull);
		RuleFor(t => t.Name)
			 .Length(4, 50)
			 .WithMessage(Resource.InvalidNameLength);

		RuleFor(t => t.Emblem)
			.NotEmpty()
			.WithMessage(Resource.EmblemNotNull);

		RuleFor(t => t.UniformHome)
			.NotEmpty()
			.WithMessage(Resource.UniformHomeNotNull);

		RuleFor(t => t.UniformAway)
			.NotEmpty()
			.WithMessage(Resource.UniformAwayNotNull);

		RuleFor(t => t.SportsId)
			.NotEmpty()
			.WithMessage(Resource.SportNotNull);
		RuleFor(t => t.SportsId)
			.Must(t => t.Equals(1) || t.Equals(2))
			.WithMessage(Resource.InvalidSport);
	}
}
