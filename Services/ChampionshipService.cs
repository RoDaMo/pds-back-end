using PlayOffsApi.Controllers.Validations;
using PlayOffsApi.Models;
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

	public async Task CreateSendAsync(Championship championship)
	{
		championship.Id = await _dbService.EditData(
			"INSERT INTO championships (name, prize, sportsid, initialdate, finaldate) VALUES (@Name, @Prize, @SportsId, @Initialdate, @Finaldate) RETURNING Id;",
			championship);

		var resultado = await _elasticService._client.IndexAsync(championship, INDEX);

		if (!resultado.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);
	}

	public async Task<List<Championship>> GetByFilterValidationAsync(string name)
		=> await GetByFilterSendAsync(name);

	public async Task<List<Championship>> GetByFilterSendAsync(string name)
		=> await _elasticService.SearchAsync<Championship>(el =>
		{
			el.Index(INDEX).From(0).Size(999);
			if (!string.IsNullOrWhiteSpace(name))
			{
				el.Query(q => q
					.MatchPhrasePrefix(m => m
						.Query(name)
						.Field(f => f.Name)
					)
				);
			}
		});
}
