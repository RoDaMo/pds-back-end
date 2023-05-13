using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Services;

public class ChampionshipService
{
	private readonly DbService _dbService;
	private readonly ElasticService _elasticService;
	private const string INDEX = "championships";

	public ChampionshipService(DbService dbService, ElasticService elasticService)
	{
		_dbService = dbService;
		_elasticService = elasticService;
	}
	public async Task<List<string>> CreateValidationAsync(Championship championship)
	{
		var errorMessages = new List<string>();

		var championshipValidator = new ChampionshipValidator();

		var result = championshipValidator.Validate(championship);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		await CreateSendAsync(championship);

		return errorMessages;
	}

	private async Task CreateSendAsync(Championship championship)
	{
		championship.Id = await _dbService.EditData(
			"INSERT INTO championships (name, prize, sportsid, initialdate, finaldate) VALUES (@Name, @Prize, @SportsId, @Initialdate, @Finaldate) RETURNING Id;",
			championship);

		var resultado = await _elasticService._client.IndexAsync(championship, INDEX);

		if (!resultado.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);
	}

	public async Task<List<Championship>> GetByFilterValidationAsync(string name, Sports sport, DateTime start, DateTime finish)
	{
		finish = finish == DateTime.MinValue ? DateTime.MaxValue : finish;
		return await GetByFilterSendAsync(name, sport, start, finish);
	}

	private async Task<List<Championship>> GetByFilterSendAsync(string name, Sports sport, DateTime start, DateTime finish)
		=> await _elasticService.SearchAsync<Championship>(el =>
		{
			el.Index(INDEX).From(0).Size(999);
			el.Query(q => q
				.Bool(b => b
					.Must(
						must =>
						{
							if (string.IsNullOrEmpty(name)) return;
							must.MatchPhrasePrefix(mpp => mpp
								.Field(f => f.Name)
								.Query(name)
							);
						},
						must2 => must2
							.Range(r => r
								.DateRange(d => d.Field(f => f.InitialDate).Gte(start).Lte(finish))
							)
					)
					.Filter(fi =>
						{
							if (sport == Sports.All) return;
							fi.Term(t => t.Field(f => f.SportsId).Value((int)sport));
						}
					)
				)
			);
		});
}
