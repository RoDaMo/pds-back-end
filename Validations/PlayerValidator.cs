using FluentValidation;
using PlayOffsApi.Models;
using Resource = PlayOffsApi.Resources.Validations.Player.Player;

namespace PlayOffsApi.Validations;

public class PlayerValidator : AbstractValidator<User>
{
    public PlayerValidator()
	{
			RuleFor(p => p.ArtisticName)
				.NotEmpty()
				.WithMessage(Resource.PlayerValidatorArtisticNameNotNull);
			RuleFor(p => p.ArtisticName)
				.Length(4, 50)
				.WithMessage(Resource.PlayerValidatorInvalidArtisticNameLength);

			RuleFor(p => p.Number)
				.NotEmpty()
				.WithMessage(Resource.PlayerValidatorNumberNotNull);
			RuleFor(p => p.Number)
				.InclusiveBetween(1, 99)
				.WithMessage(Resource.PlayerValidatorInvalidNumberLength);

			RuleFor(p => p.PlayerTeamId)
				.NotEmpty()
				.WithMessage(Resource.PlayerValidatorTeamNotNull);
			
			RuleFor(p => p.PlayerPosition)
				.NotEmpty()
				.WithMessage(Resource.PlayerValidatorPositionNotNull);
    }
}
