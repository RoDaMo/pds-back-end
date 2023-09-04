using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using PlayOffsApi.DTO;
using PlayOffsApi.Enum;
using PlayOffsApi.HostedService;
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
	private readonly IBackgroundJobsService _backgroundJobs;
	private readonly OrganizerService _organizerService;
	private const string INDEX = "championships";
     private readonly ILogger<BackgroundJob> _logger;

	public ChampionshipService(DbService dbService, ElasticService elasticService, AuthService authService, IBackgroundJobsService backgroundJobs, RedisService redisService, OrganizerService organizerService, ILogger<BackgroundJob> logger)
	{
		_dbService = dbService;
		_elasticService = elasticService;
		_authService = authService;
		_backgroundJobs = backgroundJobs;
		_redisService = redisService;
		_organizerService = organizerService;
        _logger = logger;
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

		if (!await _authService.UserHasCpfValidationAsync(championship.Organizer.Id) && !await _authService.UserHasCnpjValidationAsync(championship.Organizer.Id))
			throw new ApplicationException(Resource.CreateValidationAsyncCpfNotNull);

		championship.Status = ChampionshipStatus.Pendent;
		
		switch (championship.SportsId)
		{
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
			INSERT INTO championships (name, sportsid, initialdate, finaldate, logo, description, format, organizerId, teamquantity, status, doublematchgroupstage, doublematcheliminations, doublestartleaguesystem, finaldoublematch, deleted) 
			VALUES (@Name, @SportsId, @Initialdate, @Finaldate, @Logo, @Description, @Format, @OrganizerId, @TeamQuantity, @Status, @DoubleMatchGroupStage, @DoubleMatchEliminations, @DoubleStartLeagueSystem, @FinalDoubleMatch, false) RETURNING Id;",
			championship);

		// await _dbService.EditData(
		// 	"UPDATE users SET championshipId = @championshipId WHERE id = @userId", new
		// 		{ championshipId = championship.Id, userId = championship.Organizer.Id });

		var resultado = await _elasticService._client.IndexAsync(championship, INDEX);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);

		await _organizerService.InsertValidation(new Organizer { ChampionshipId = championship.Id, MainOrganizer = true, OrganizerId = championship.Organizer.Id });

		
		_logger.LogInformation("Campeonato criado");
		await _backgroundJobs.EnqueueJob(() => _backgroundJobs.ChangeChampionshipStatusValidation(championship.Id, (int)ChampionshipStatus.Inactive), TimeSpan.FromDays(14));
		await _backgroundJobs.EnqueueJob(() => _backgroundJobs.ChangeChampionshipStatusValidation(championship.Id, (int)ChampionshipStatus.Active), championship.InitialDate - DateTime.UtcNow);
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
							must.Match(mpp => mpp
								.Field(f => f.Name)
								.Query(name)
								.Fuzziness(new Fuzziness("Auto"))
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
		=> await _dbService.GetAsync<Championship>("SELECT id, name, sportsid, initialdate, finaldate, rules, logo, description, format, organizerid, teamquantity, doublematchgroupstage, doublematcheliminations, doublestartleaguesystem, finaldoublematch FROM championships WHERE id = @id", new { id });
	
	private async Task<int> GetNumberOfPlayers(int championshipId)
		=> await _dbService.GetAsync<int>("SELECT numberofplayers FROM championships WHERE id = @championshipId", new {championshipId});
		
	public async Task<List<string>> UpdateValidate(Championship championship)
	{
		var oldChamp = await GetByIdValidation(championship.Id);
		if (oldChamp is null)
			throw new ApplicationException(Resource.InvalidChampionship);

		if (oldChamp.Status == ChampionshipStatus.Pendent && oldChamp.InitialDate != championship.InitialDate)
		{
			await using var redisDatabase = await _redisService.GetDatabase();
			await redisDatabase.AddAsync($"cancelJob_championship:{championship.Id}", oldChamp.InitialDate);
			
			await _backgroundJobs.EnqueueJob(() => _backgroundJobs.ChangeChampionshipStatusValidation(championship.Id, (int)ChampionshipStatus.Active), championship.InitialDate - DateTime.UtcNow);
		}

		var result = await new ChampionshipValidator().ValidateAsync(championship);
		
		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();
		
		if(oldChamp.DoubleMatchEliminations != championship.DoubleMatchEliminations ||
			oldChamp.DoubleMatchGroupStage != championship.DoubleMatchGroupStage ||
			oldChamp.DoubleStartLeagueSystem != championship.DoubleStartLeagueSystem ||
			oldChamp.FinalDoubleMatch != championship.FinalDoubleMatch ||
			oldChamp.Format != championship.Format)
		{
			if(await CheckIfChampionshipHasAnyMatch(oldChamp.Id))
				throw new ApplicationException("Não é possível alterar o formato da competição após a definição do chaveamento.");
		}
		
		await UpdateSend(championship);
		await _elasticService._client.IndexAsync(championship, INDEX);

		return new();
	}

	private async Task<bool> CheckIfChampionshipHasAnyMatch(int championshipId)
		=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Matches WHERE championshipId = @championshipId)", new {championshipId});
	private async Task UpdateSend(Championship championship) =>
		await _dbService.EditData(
			"UPDATE championships SET " +
			"name = @name, initialdate = @initialdate, finaldate = @finaldate, rules = @rules, logo = @logo, description = @description, format = @format, teamquantity = @teamquantity, doublematchgroupstage = @doublematchgroupstage, doublematcheliminations = @doublematcheliminations, doublestartleaguesystem = @doublestartleaguesystem, finaldoublematch = @finaldoublematch " +
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
		await _organizerService.DeleteValidation(new() { ChampionshipId = championship.Id, OrganizerId = championship.OrganizerId });
	}

	private async Task DeleteSend(Championship championship)
	{
		await _dbService.EditData("UPDATE championships SET deleted = true WHERE id = @id", championship);
		await _dbService.EditData("UPDATE users SET championshipid = null WHERE id = @organizerId", championship);
	}

	public async Task<bool> CanMoreTeamsBeAddedValidation(int championshipId) => await CanMoreTeamsBeAddedSend(championshipId);

	private async Task<bool> CanMoreTeamsBeAddedSend(int championshipId) => await _dbService.GetAsync<bool>("SELECT COALESCE((SELECT COUNT(*) FROM championships_teams WHERE championshipid = @championshipId) < teamquantity, 'true') FROM championships WHERE id = @championshipId;", new { championshipId });

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

	public async Task<List<MatchDTO>> GetAllMatchesByRoundValidation(int championshipId, int round)
	{
		var championship = await GetByIdSend(championshipId);
		
		if(championship is null)
			throw new ApplicationException("Campeonato passado não existe");
		
		if(championship.Format == Format.Knockout)
			throw new ApplicationException("Formato de campeonato inválido");
		
		var matches = await GetMatchesByRoundAndChampionship(championshipId, round);
		var matchesDTO = new List<MatchDTO>();

		if(championship.SportsId == Sports.Football)
		{
			foreach (var match in matches)
			{
				var matchDTO = new MatchDTO();
				var home = await GetByTeamIdSendAsync(match.Home);
				var visitor = await GetByTeamIdSendAsync(match.Visitor);
				matchDTO.Id = match.Id;
				matchDTO.IsSoccer = true;
				matchDTO.HomeEmblem = home.Emblem;
				matchDTO.HomeName = home.Name;
				matchDTO.HomeId = home.Id;
				matchDTO.HomeGoals = await GetPointsFromTeamById(match.Id, match.Home);
				matchDTO.VisitorEmblem = visitor.Emblem;
				matchDTO.VisitorName = visitor.Name;
				matchDTO.VisitorGoals = await GetPointsFromTeamById(match.Id, match.Visitor);
				matchDTO.VisitorId = visitor.Id;
				matchDTO.Cep = match.Cep;
				matchDTO.City = match.City;
				matchDTO.Road = match.Road;
				matchDTO.Number = match.Number;
				matchDTO.MatchReport = match.MatchReport;
				matchDTO.Arbitrator = match.Arbitrator;
				matchDTO.Date = match.Date;
				matchDTO.Finished = (match.Winner != 0 || match.Tied == true) ? true : false;
				matchesDTO.Add(matchDTO);
			}
			return matchesDTO;
		}

		else
		{
			foreach (var match in matches)
			{
				var matchDTO = new MatchDTO();
				var homeTeam = await GetByTeamIdSendAsync(match.Home);
				var visitorTeam = await GetByTeamIdSendAsync(match.Visitor);
				matchDTO.Id = match.Id;
				matchDTO.HomeEmblem = homeTeam.Emblem;
				matchDTO.HomeName = homeTeam.Name;
				matchDTO.HomeId = homeTeam.Id;
				matchDTO.VisitorId = visitorTeam.Id;
				matchDTO.VisitorEmblem = visitorTeam.Emblem;
				matchDTO.VisitorName = visitorTeam.Name;
				matchDTO.Cep = match.Cep;
				matchDTO.City = match.City;
				matchDTO.Road = match.Road;
				matchDTO.Number = match.Number;
				matchDTO.MatchReport = match.MatchReport;
				matchDTO.Arbitrator = match.Arbitrator;
				matchDTO.Date = match.Date;
				matchDTO.Finished = (match.Winner != 0 || match.Tied == true) ? true : false;
				var pointsForSet = new List<int>();
				var pointsForSet2 = new List<int>();
				var WonSets = 0;
				var WonSets2 = 0;
				var lastSet = 0;
				lastSet = !await IsItFirstSet(match.Id) ? 1 : await GetLastSet(match.Id);
				var team2Id = await _dbService.GetAsync<int>("SELECT CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches WHERE id = @matchId;", new {teamId = match.Home, matchId = match.Id});

				for (int i = 0;  i < lastSet; i++)
				{
					pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId And OwnGoal = false OR TeamId <> @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId = match.Home, j = i+1}));
					pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId And OwnGoal = false OR TeamId = @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId = match.Home, j = i+1}));
				}

				for (int i = 0;  i < lastSet; i++)
				{
					if(i != 4)
					{
						if(pointsForSet[i] == 25 && pointsForSet2[i] < 24)
						{
							WonSets++;
						}
						else if(pointsForSet[i] < 24 && pointsForSet2[i] == 25)
						{
							WonSets2++;
						}
						else if(pointsForSet[i] >= 24 && pointsForSet2[i] >= 24)
						{
							if(pointsForSet[i] - pointsForSet2[i] == 2)
							{
								WonSets++;
							}
							else if(pointsForSet[i] - pointsForSet2[i] == -2)
							{
								WonSets2++;

							}
						}
					}

					else
					{
						if(pointsForSet[i] == 15 && pointsForSet2[i] < 14)
						{
							WonSets++;
						}
						else if(pointsForSet[i] < 14 && pointsForSet2[i] == 15)
						{
							WonSets2++;
						}
						else if(pointsForSet[i] >= 14 && pointsForSet2[i] >= 14)
						{
							if(pointsForSet[i] - pointsForSet2[i] == 2)
							{
								WonSets++;
							}
							else if(pointsForSet[i] - pointsForSet2[i] == -2)
							{
								WonSets2++;

							}
						}
					}
				}
				matchDTO.HomeWinnigSets = WonSets;
				matchDTO.VisitorWinnigSets = WonSets2;
				matchesDTO.Add(matchDTO);
			}
		}
		return matchesDTO;
	}

	private async Task<List<Match>> GetMatchesByRoundAndChampionship(int championshipId, int round)
		=> await _dbService.GetAll<Match>("SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Round = @round ORDER BY Id", new {championshipId, round});
	
	private async Task<Team> GetByTeamIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id AND deleted = false", new {id});

	private async Task<int> GetPointsFromTeamById(int matchId, int teamId)
        => await _dbService.GetAsync<int>("SELECT COUNT(*) FROM goals WHERE MatchId = @matchId AND (TeamId = @teamId AND OwnGoal = false OR TeamId <> @teamId AND OwnGoal = true)", new {matchId, teamId});
  
   private async Task<bool> IsItFirstSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM goals WHERE MatchId = @matchId);", new {matchId});
   
   private async Task<int> GetLastSet(int matchId)
    	=> await _dbService.GetAsync<int>("SELECT MAX(Set) from goals where MatchId = @matchId", new {matchId});
	
	public async Task<List<MatchDTO>> GetAllMatchesByPhaseValidation(int championshipId, int phase)
	{
		var championship = await GetByIdSend(championshipId);
		
		if(championship is null)
			throw new ApplicationException("Campeonato passado não existe");
		
		if(championship.Format == Format.LeagueSystem)
			throw new ApplicationException("Formato de campeonato inválido");
		
		var matches = await GetMatchesByPhaseAndChampionship(championshipId, phase);
		var matchesDTO = new List<MatchDTO>();

		if(championship.SportsId == Sports.Football)
		{
			foreach (var match in matches)
			{
				var matchDTO = new MatchDTO();
				var home = await GetByTeamIdSendAsync(match.Home);
				var visitor = await GetByTeamIdSendAsync(match.Visitor);
				matchDTO.Id = match.Id;
				matchDTO.IsSoccer = true;
				matchDTO.HomeEmblem = home.Emblem;
				matchDTO.HomeName = home.Name;
				matchDTO.HomeId = home.Id;
				matchDTO.HomeGoals = await GetPointsFromTeamById(match.Id, match.Home);
				matchDTO.VisitorEmblem = visitor.Emblem;
				matchDTO.VisitorName = visitor.Name;
				matchDTO.VisitorId = visitor.Id;
				matchDTO.Cep = match.Cep;
				matchDTO.City = match.City;
				matchDTO.Road = match.Road;
				matchDTO.Number = match.Number;
				matchDTO.MatchReport = match.MatchReport;
				matchDTO.Arbitrator = match.Arbitrator;
				matchDTO.Date = match.Date;
				matchDTO.VisitorGoals = await GetPointsFromTeamById(match.Id, match.Visitor);
				matchDTO.Finished = (match.Winner != 0 || match.Tied == true) ? true : false;
				if(match.PreviousMatch != 0)
					matchDTO.WinnerName = (match.Winner == home.Id) ? home.Name : visitor.Name;
				matchesDTO.Add(matchDTO);
			}
			return matchesDTO;
		}

		else
		{
			foreach (var match in matches)
			{
				var matchDTO = new MatchDTO();
				var homeTeam = await GetByTeamIdSendAsync(match.Home);
				var visitorTeam = await GetByTeamIdSendAsync(match.Visitor);
				matchDTO.Id = match.Id;
				matchDTO.HomeEmblem = homeTeam.Emblem;
				matchDTO.HomeName = homeTeam.Name;
				matchDTO.HomeId = homeTeam.Id;
				matchDTO.VisitorEmblem = visitorTeam.Emblem;
				matchDTO.VisitorName = visitorTeam.Name;
				matchDTO.VisitorId = visitorTeam.Id;
				matchDTO.Cep = match.Cep;
				matchDTO.City = match.City;
				matchDTO.Road = match.Road;
				matchDTO.Number = match.Number;
				matchDTO.MatchReport = match.MatchReport;
				matchDTO.Arbitrator = match.Arbitrator;
				matchDTO.Date = match.Date;
				matchDTO.Finished = (match.Winner != 0 || match.Tied == true) ? true : false;
				var pointsForSet = new List<int>();
				var pointsForSet2 = new List<int>();
				var WonSets = 0;
				var WonSets2 = 0;
				var lastSet = 0;
				lastSet = !await IsItFirstSet(match.Id) ? 1 : await GetLastSet(match.Id);
				var team2Id = await _dbService.GetAsync<int>("SELECT CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches WHERE id = @matchId;", new {teamId = match.Home, matchId = match.Id});

				for (int i = 0;  i < lastSet; i++)
				{
					pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId And OwnGoal = false OR TeamId <> @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId = match.Home, j = i+1}));
					pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId And OwnGoal = false OR TeamId = @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId = match.Home, j = i+1}));
				}

				for (int i = 0;  i < lastSet; i++)
				{
					if(i != 4)
					{
						if(pointsForSet[i] == 25 && pointsForSet2[i] < 24)
						{
							WonSets++;
						}
						else if(pointsForSet[i] < 24 && pointsForSet2[i] == 25)
						{
							WonSets2++;
						}
						else if(pointsForSet[i] >= 24 && pointsForSet2[i] >= 24)
						{
							if(pointsForSet[i] - pointsForSet2[i] == 2)
							{
								WonSets++;
							}
							else if(pointsForSet[i] - pointsForSet2[i] == -2)
							{
								WonSets2++;

							}
						}
					}

					else
					{
						if(pointsForSet[i] == 15 && pointsForSet2[i] < 14)
						{
							WonSets++;
						}
						else if(pointsForSet[i] < 14 && pointsForSet2[i] == 15)
						{
							WonSets2++;
						}
						else if(pointsForSet[i] >= 14 && pointsForSet2[i] >= 14)
						{
							if(pointsForSet[i] - pointsForSet2[i] == 2)
							{
								WonSets++;
							}
							else if(pointsForSet[i] - pointsForSet2[i] == -2)
							{
								WonSets2++;

							}
						}
					}
				}
				matchDTO.HomeWinnigSets = WonSets;
				matchDTO.VisitorWinnigSets = WonSets2;
				matchesDTO.Add(matchDTO);
			}
		}
		return matchesDTO;
	}

	private async Task<List<Match>> GetMatchesByPhaseAndChampionship(int championshipId, int phase)
		=> await _dbService.GetAll<Match>("SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Phase = @phase ORDER BY Id", new {championshipId, phase});
	
}
