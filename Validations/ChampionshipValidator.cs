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
			.WithMessage("Campo Esporte não pode ser vazio.");
		
		RuleFor(c => c.SportsId)
			.Must(c => c.Equals(Sports.Football) || c.Equals(Sports.Volleyball))
			.WithMessage("Campo Esporte deve ser preenchido com Vôlei ou futebol.");

		RuleFor(c => c.TeamQuantity)
			.Must(IsPowerOfTwo)
			.WithMessage("Quantidade de times deve ser um quadrado de 2, como 4, 16, 32, 64, etc.");

		RuleFor(c => c.TeamQuantity)
			.LessThanOrEqualTo(128)
			.WithMessage("Quantidade de times não pode exceder 128 times.");

		RuleFor(c => c.TeamQuantity)
			.GreaterThanOrEqualTo(2)
			.WithMessage("Deve haver pelo menos 2 times no campeonato.");

		RuleFor(c => c.Description)
			.Length(10, 10000)
			.WithMessage("Deve haver pelo menos 10 caracteres na descrição, e no máximo 10 mil caracteres.");

		RuleFor(c => c.Nation)
			.NotEmpty()
			.WithMessage("Campo País não pode estar vazio.");

		RuleFor(c => c.State)
			.NotEmpty()
			.WithMessage("Campo Estado não pode estar vazio.");
		
		RuleFor(c => c.City)
			.NotEmpty()
			.WithMessage("Campo Cidade não pode estar vazia.");

		RuleFor(c => c.Neighborhood)
			.NotEmpty()
			.WithMessage("Campo Bairro não pode estar vazio.");

		RuleFor(c => c.NumberOfPlayers)
			.NotEmpty()
			.WithMessage("Campo Número de Jogadores não pode estar vazio.");
	}

	private static bool IsPowerOfTwo(int x) => x is not 0 && (x & (x - 1)) == 0;
}