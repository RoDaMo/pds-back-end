using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
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
			@"
			INSERT INTO championships (name, sportsid, initialdate, finaldate, logo, description, format, nation, state, city, neighborhood) 
			VALUES (@Name, @SportsId, @Initialdate, @Finaldate, @Logo, @Description, @Format, @Nation, @State, @City, @Neighborhood) RETURNING Id;",
			championship);

		var resultado = await _elasticService._client.IndexAsync(championship, INDEX);

		if (!resultado.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);
	}

	public async Task<List<Championship>> GetByFilterValidationAsync(string name, Sports sport, DateTime start, DateTime finish, string pitId, string[] sort)
	{
		finish = finish == DateTime.MinValue ? DateTime.MaxValue : finish;
		var pit = string.IsNullOrEmpty(pitId)
			? await _elasticService.OpenPointInTimeAsync(Indices.Index(INDEX))
			: new() { Id = pitId, KeepAlive = 120000 };

		var listSort = new List<FieldValue>();
		if (sort is not null && sort.Any())
			listSort = sort.Select(FieldValue.String).ToList();
		
		var response = await GetByFilterSendAsync(name, sport, start, finish, pit, listSort);
		var documents = response.Documents.ToList();
		documents.Last().PitId = response.PitId;
		documents.Last().Sort = response.Hits.Last().Sort;
		
		return documents;
	}

	private async Task<SearchResponse<Championship>> GetByFilterSendAsync(string name, Sports sport, DateTime start, DateTime finish, PointInTimeReference pitId, ICollection<FieldValue> sort)
		=> await _elasticService.SearchAsync<Championship>(el =>
		{
			el.Index(INDEX).From(0).Size(15).Pit(pitId).Sort(config => config.Score(new ScoreSort { Order = SortOrder.Desc }));
			
			if (sort.Any()) el.SearchAfter(sort);
			
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

	public async Task<Championship> GetByIdValidation(int id) => await GetByIdSend(id);

	private async Task<Championship> GetByIdSend(int id) 
		=> await _dbService.GetAsync<Championship>("SELECT id, name, sportsid, initialdate, finaldate, rules, logo, description, format, nation, state, city, neighborhood FROM championships WHERE id = @id", id);
}
