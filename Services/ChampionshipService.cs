using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Generic = PlayOffsApi.Resources.Generic;
using Resource = PlayOffsApi.Resources.Services.ChampionshipService;

namespace PlayOffsApi.Services;

public class ChampionshipService
{
	private readonly DbService _dbService;
	private readonly ElasticService _elasticService;
	private readonly RedisService _redisService;
	private readonly AuthService _authService;  
	private readonly BackgroundService _backgroundService;
	private const string INDEX = "championships";

	public ChampionshipService(DbService dbService, ElasticService elasticService, AuthService authService, BackgroundService backgroundService, RedisService redisService)
	{
		_dbService = dbService;
		_elasticService = elasticService;
		_authService = authService;
		_backgroundService = backgroundService;
		_redisService = redisService;
	}
	public async Task<List<string>> CreateValidationAsync(Championship championship)
	{
		var errorMessages = new List<string>();

		var championshipValidator = new ChampionshipValidator();
		var result = await championshipValidator.ValidateAsync(championship);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if (!await _authService.UserHasCpfValidationAsync(championship.Organizer.Id))
			throw new ApplicationException(Resource.CreateValidationAsyncCpfNotNull);

		championship.Status = ChampionshipStatus.Pendent;
		
		switch (championship.SportsId)
		{
			case Sports.Football when championship.NumberOfPlayers < 11:
				throw new ApplicationException(Resource.CreateValidationAsyncInvalidFootballPlayers);
			case Sports.Volleyball when championship.NumberOfPlayers < 6:
				throw new ApplicationException(Resource.CreateValidationAsyncInvalidVolleyPlayers);
			case Sports.All:
				throw new ApplicationException(Resource.CreateValidationAsyncInvalidSport);
			default:
				await CreateSendAsync(championship);
				return errorMessages;
		}
	}

	private async Task CreateSendAsync(Championship championship)
	{
		championship.Id = await _dbService.EditData(
			@"
			INSERT INTO championships (name, sportsid, initialdate, finaldate, logo, description, format, nation, state, city, neighborhood, organizerId, numberofplayers, teamquantity, status, doublematchgroupstage, doublematcheliminations, doublestartleaguesystem, finaldoublematch) 
			VALUES (@Name, @SportsId, @Initialdate, @Finaldate, @Logo, @Description, @Format, @Nation, @State, @City, @Neighborhood, @OrganizerId, @NumberOfPlayers, @TeamQuantity, @Status, @DoubleMatchGroupStage, @DoubleMatchEliminations, @DoubleStartLeagueSystem, @FinalDoubleMatch) RETURNING Id;",
			championship);

		await _dbService.EditData(
			"UPDATE users SET championshipId = @championshipId WHERE id = @userId", new
				{ championshipId = championship.Id, userId = championship.Organizer.Id });

		var resultado = await _elasticService._client.IndexAsync(championship, INDEX);

		if (!resultado.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);

		await _backgroundService.EnqueueJob(nameof(_backgroundService.ChangeChampionshipStatusValidation), new object[] { championship.Id, ChampionshipStatus.Inactive }, TimeSpan.FromDays(14));
		await _backgroundService.EnqueueJob(nameof(_backgroundService.ChangeChampionshipStatusValidation), new object[] { championship.Id, ChampionshipStatus.Active }, championship.InitialDate - DateTime.UtcNow);
	}

	public async Task<(List<Championship> campionships, long total)> GetByFilterValidationAsync(string name, Sports sport, DateTime start, DateTime finish, string pitId, string[] sort, ChampionshipStatus status)
	{
		finish = finish == DateTime.MinValue ? DateTime.MaxValue : finish;
		var pit = string.IsNullOrEmpty(pitId)
			? await _elasticService.OpenPointInTimeAsync(Indices.Index(INDEX))
			: new() { Id = pitId, KeepAlive = 120000 };

		var listSort = new List<FieldValue>();
		if (sort is not null && sort.Any())
			listSort = sort.Select(FieldValue.String).ToList();
		
		var response = await GetByFilterSendAsync(name, sport, start, finish, pit, listSort, status);
		var documents = response.Documents.ToList();
		if (!documents.Any()) return (documents, 0);
		
		documents.Last().PitId = response.PitId;
		documents.Last().Sort = response.Hits.Last().Sort;

		return (documents, response.Total);
	}

	private async Task<SearchResponse<Championship>> GetByFilterSendAsync(string name, Sports sport, DateTime start,
		DateTime finish, PointInTimeReference pitId, ICollection<FieldValue> sort,
		ChampionshipStatus championshipStatus)
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
							),
						must3 => must3
							.Term(t => t.Field(f => f.Deleted).Value(false)),
						must4 => must4.Term(t => t.Field(f => f.Status).Value((int)championshipStatus)),
						must5 => must5.Range(r => r
							.DateRange(d => d.Field(f => f.FinalDate).Gte(start).Lte(finish))
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

	public async Task<Championship> GetByIdValidation(int id)
	{
		var championship = await GetByIdSend(id);
		championship.Teams = await GetAllTeamsOfChampionshipValidation(id);
		return championship;
	}

	private async Task<Championship> GetByIdSend(int id) 
		=> await _dbService.GetAsync<Championship>("SELECT id, name, sportsid, initialdate, finaldate, rules, logo, description, format, nation, state, city, neighborhood, organizerid, teamquantity, numberofplayers FROM championships WHERE id = @id", new { id });
	
	private async Task<int> GetNumberOfPlayers(int championshipId)
		=> await _dbService.GetAsync<int>("SELECT numberofplayers FROM championships WHERE id = @championshipId", new {championshipId});
		
	public async Task<List<string>> UpdateValidate(Championship championship)
	{
		var oldChamp = await GetByIdValidation(championship.Id);
		if (oldChamp is null)
			throw new ApplicationException(Resource.InvalidChampionship);

		if (oldChamp.Status == ChampionshipStatus.Pendent && oldChamp.InitialDate != championship.InitialDate)
		{
			var redisDatabase = await _redisService.GetDatabase();
			await redisDatabase.AddAsync($"cancelJob_championship:{championship.Id}", oldChamp.InitialDate);
			
			await _backgroundService.EnqueueJob(nameof(_backgroundService.ChangeChampionshipStatusValidation), new object[] { championship.Id, ChampionshipStatus.Active }, championship.InitialDate - DateTime.UtcNow);
		}

		var result = await new ChampionshipValidator().ValidateAsync(championship);
		
		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();
		
		await UpdateSend(championship);
		await _elasticService._client.IndexAsync(championship, INDEX);

		return new();
	}

	private async Task UpdateSend(Championship championship) =>
		await _dbService.EditData(
			"UPDATE championships SET " +
			"name = @name, initialdate = @initialdate, finaldate = @finaldate, rules = @rules, logo = @logo, description = @description, format = @format, nation = @nation, state = @state, city = @city, neighborhood = @neighborhood, teamquantity = @teamquantity, numberofplayers = @numberofplayers " +
			"WHERE id=@id",
			championship);

	public async Task<List<Team>> GetAllTeamsOfChampionshipValidation(int championshipId) => await GetAllTeamsOfChampionshipSend(championshipId);

	private async Task<List<Team>> GetAllTeamsOfChampionshipSend(int championshipId)
		=> await _dbService.GetAll<Team>("SELECT c.emblem, c.name, c.id FROM teams c JOIN championships_teams ct ON c.id = ct.teamId AND ct.championshipid = @championshipId;", new { championshipId });

	public async Task DeleteValidation(Championship championship)
	{
		await DeleteSend(championship);
		championship.Deleted = true;
		await _elasticService._client.IndexAsync(championship, INDEX);
	}

	private async Task DeleteSend(Championship championship)
	{
		await _dbService.EditData("UPDATE championships SET deleted = true WHERE id = @id", championship);
		await _dbService.EditData("UPDATE users SET championshipid = null WHERE id = @organizerId", championship);
	}

	public async Task<bool> CanMoreTeamsBeAddedValidation(int championshipId) => await CanMoreTeamsBeAddedSend(championshipId);

	private async Task<bool> CanMoreTeamsBeAddedSend(int championshipId) => await _dbService.GetAsync<bool>("SELECT COALESCE((SELECT COUNT(*) FROM championships_teams) <= teamquantity, 'true') FROM championships WHERE id = @championshipId;", new { championshipId });

	public async Task<List<int>> GetAllTeamsLinkedToValidation(int championshipId) =>
		await GetAllTeamsLinkedToSend(championshipId);

	private async Task<List<int>> GetAllTeamsLinkedToSend(int championshipId) => await _dbService.GetAll<int>("SELECT teamid FROM championships_teams WHERE championshipid = @championshipid;", new { championshipId });

	public async Task IndexAllChampionshipsValidation()
	{
		var championships = await GetAllChampionshipsForIndexingSend();
		foreach (var championship in championships)
			await _elasticService._client.IndexAsync(championship, INDEX);
	}

	private async Task<List<Championship>> GetAllChampionshipsForIndexingSend() => await _dbService.GetAll<Championship>("SELECT * FROM championships", new {});
}
