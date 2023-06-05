using FluentValidation;
using PlayOffsApi.Models;
using Resource = PlayOffsApi.Resources.Validations.PlayerTempProfile.PlayerTempProfile;

namespace PlayOffsApi.Validations;

public class PlayerTempProfileValidator : AbstractValidator<PlayerTempProfile>
{
    public PlayerTempProfileValidator()
	{
			RuleFor(p => p.Name)
				.NotEmpty()
				.WithMessage(Resource.NameFieldNotNull);
			RuleFor(p => p.Name)
				.Length(4, 50)
				.WithMessage(Resource.InvalidNameLength);

			RuleFor(p => p.ArtisticName)
				.NotEmpty()
				.WithMessage(Resource.ArtisticNameNotNull);
			RuleFor(p => p.ArtisticName)
				.Length(4, 50)
				.WithMessage(Resource.InvalidArtisticNameLength);
			
			RuleFor(p => p.Email)
				.NotEmpty()
				.EmailAddress()
				.WithMessage(Resource.InvalidEmail);

			RuleFor(p => p.Number)
				.NotEmpty()
				.WithMessage(Resource.NumberNotNull);
			RuleFor(p => p.Number)
				.InclusiveBetween(1, 99)
				.WithMessage(Resource.InvalidNumberLengthTwo);

			RuleFor(p => p.TeamsId)
				.NotEmpty()
				.WithMessage(Resource.TeamNotNull);
			
			RuleFor(p => p.PlayerPosition)
				.NotEmpty()
				.WithMessage(Resource.PositionNotNull);
		}	
	
}
