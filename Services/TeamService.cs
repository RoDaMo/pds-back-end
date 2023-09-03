using Elastic.Clients.Elasticsearch;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.TeamService;
using Generic = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Services;

public class TeamService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
    private readonly AuthService _authService;
    private readonly ChampionshipService _championshipService;
	private const string INDEX = "teams";
	private const string INDEX2 = "users";
	private readonly BracketingMatchService _bracketingMatchService;
	
	
    public TeamService(DbService dbService, ElasticService elasticService, AuthService authService,
	 ChampionshipService championshipService, BracketingMatchService bracketingMatchService)
	{
		_dbService = dbService;
        _elasticService = elasticService;
        _authService = authService;
        _championshipService = championshipService;
		_bracketingMatchService = bracketingMatchService;
	}

    public async Task<List<string>> CreateValidationAsync(TeamDTO teamDto, Guid userId)
	{
		var errorMessages = new List<string>();
		var teamValidator = new TeamValidator();

		var result = await teamValidator.ValidateAsync(teamDto);
		if (!await _authService.UserHasCpfValidationAsync(userId))
			throw new ApplicationException(Resource.CreateValidationAsyncCpfNeeded);
		
		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await IsAlreadyTechOfAnotherTeam(userId))
			throw new ApplicationException(Resource.CreateValidationAsyncAlreadyCoach);
		
		var team = ToTeam(teamDto);

		team.Id =  await CreateSendAsync(team);
		await UpdateUser(userId, team.Id);

		var resultado = await _elasticService._client.IndexAsync(team, INDEX);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);
		
		return errorMessages;
	}


    private async Task<int> CreateSendAsync(Team team) => await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformAway, deleted, sportsid, name) VALUES (@Emblem, @UniformHome, @UniformAway, @Deleted, @SportsId, @Name) RETURNING Id;",
			team);

	public async Task<List<Team>> GetAllValidationAsync(Sports sport) => await GetAllSendAsync(sport);

	private async Task<List<Team>> GetAllSendAsync(Sports sport) => await _dbService.GetAll<Team>("SELECT * FROM teams WHERE sportId = @sport AND Deleted <> true", new { sport });

	public async Task<Team> GetByIdValidationAsync(int id)
	{
		var team = await GetByIdSendAsync(id);
		if (team is null)
			return null;
		
		team.Technician = await GetTechnicianFromTeam(team.Id);
		return team;
	}

	public async Task<Team> GetByIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id AND deleted = false", new {id});

	private async Task<bool> IsAlreadyTechOfAnotherTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NOT NULL OR TeamManagementId <> 0);", new {userId});

	private async Task UpdateUser(User user)
	{
		user.TeamManagementId = 0;
		var resultado = await _elasticService._client.IndexAsync(user, INDEX2);
		await _dbService.EditData("UPDATE users SET teammanagementid = null  WHERE id = @userid;", new { userId = user.Id });
	}
	private async Task UpdateUser(Guid userId, int teamId)
	{
		var user = await _authService.GetUserByIdAsync(userId);
		user.TeamManagementId = teamId;
		var resultado = await _elasticService._client.IndexAsync(user, INDEX2);
		await _dbService.EditData("UPDATE users SET teammanagementid = @teamId  WHERE id = @userid;", new { userId, teamId });
	}

	private static Team ToTeam(TeamDTO teamDto) => new(teamDto.Emblem, teamDto.UniformHome, teamDto.UniformAway, teamDto.SportsId, teamDto.Name);

	public async Task<List<Team>> SearchTeamsValidation(string query, Sports sport, int championshipId)
	{
		var response = await SearchTeamsSend(query, sport);
		var linkedTeams = await _championshipService.GetAllTeamsLinkedToValidation(championshipId);
		var hashSet = linkedTeams.ToHashSet();
		
		var responseList = response.Documents.ToList();
		responseList.RemoveAll(r => hashSet.Contains(r.Id));
		return responseList;
	}

	private async Task<SearchResponse<Team>> SearchTeamsSend(string query, Sports sports)
		=> await _elasticService.SearchAsync<Team>(el =>
		{
			el.Index(INDEX);
			el.Query(q => q.Bool(b => b.
					Must(
						must => must.MatchPhrasePrefix(mpp => mpp.Field(f => f.Name).Query(query)),
						must2 => must2.Term(t => t.Field(f => f.Deleted).Value(false)),
						must3 => must3.Term(t => t.Field(f => f.SportsId).Value((int)sports))
					)
				)
			);
		});


	public async Task AddTeamToChampionshipValidation(int teamId, int championshipId)
	{
		if (await RelationAlreadyExistsValidation(teamId, championshipId))
			throw new ApplicationException(Resource.AddTeamToChampionshipValidationTeamAlreadyLinked);

		if (!await _championshipService.CanMoreTeamsBeAddedValidation(championshipId))
			throw new ApplicationException("O limite de times para esse campeonato já foi atingido");
			
		await AddTeamToChampionshipSend(teamId, championshipId);
	}

	private async Task AddTeamToChampionshipSend(int teamId, int championshipId)
	{
		await _dbService.EditData(
			"INSERT INTO championships_teams (teamId, championshipId) VALUES (@teamId, @championshipId)",
			new { teamId, championshipId });
	}

	public async Task<bool> RelationAlreadyExistsValidation(int teamId, int championshipId)
		=> await RelationAlreadyExistsSend(teamId, championshipId);

	private async Task<bool> RelationAlreadyExistsSend(int teamId, int championshipId)
		=> await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM championships_teams WHERE teamId = @teamId AND championshipId = @championshipId", new { teamId, championshipId });

	public async Task RemoveTeamFromChampionshipValidation(int teamId, int championshipId)
	{
		if (!await RelationAlreadyExistsValidation(teamId, championshipId))
			throw new ApplicationException(Resource.RemoveTeamFromChampionshipValidationTeamNotLinked);
		
		var championship = await _championshipService.GetByIdValidation(championshipId);

		//WO
		//checar se tem chaveamento
		//checar se o status é igual a 0 ou 3

		if(await BracketingExists(championshipId) && 
		(championship.Status == Enum.ChampionshipStatus.Active || championship.Status == Enum.ChampionshipStatus.Pendent) &&
		championship.Deleted == false)
		{
			var matches = await _dbService.GetAll<Match>(
				@"SELECT * FROM Matches WHERE (Visitor = @teamId OR Home = @teamId) AND ChampionshipId = @championshipId AND Winner IS NULL AND Tied <> true", new {teamId, championshipId});
			
			foreach (var match in matches)
			{
				await _bracketingMatchService.WoValidation(match.Id, match.Visitor == teamId ? match.Home : match.Visitor);
			}
		}

		await RemoveTeamFromChampionshipSend(teamId, championshipId);
	}
	public async Task<bool> BracketingExists(int championshipId)
	=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Matches WHERE championshipId = @championshipId)", new {championshipId});

	private async Task RemoveTeamFromChampionshipSend(int teamId, int championshipId)
		=> await _dbService.EditData("DELETE FROM championships_teams WHERE teamId = @teamId AND championshipId = @championshipId", new { teamId, championshipId });

	public async Task<List<Championship>> GetChampionshipsOfTeamValidation(int id) => await GetChampionshipsOfTeamSend(id);

	private async Task<List<Championship>> GetChampionshipsOfTeamSend(int id)
		=> await _dbService.GetAll<Championship>("SELECT c.id, c.name, c.logo, c.description, c.format, c.sportsid FROM championships c JOIN championships_teams ct ON c.id = ct.championshipid WHERE ct.teamid = @id", new { id });

	public async Task<List<string>> UpdateTeamValidation(TeamDTO teamDto, Guid userId)
	{
		var errorMessages = new List<string>();
		var teamValidator = new TeamValidator();
		
		var result = await teamValidator.ValidateAsync(teamDto);
		var user = await _authService.GetUserByIdAsync(userId);
		if (!user.TeamManagementId.Equals(teamDto.Id))
			throw new ApplicationException(Resource.UserNotAllowedToUpdate);
		
		
		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		var team = ToTeam(teamDto);
		team.Id = teamDto.Id;
		await UpdateTeamSend(team);
		
		var resultado = await _elasticService._client.IndexAsync(team, INDEX);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);
		
		return errorMessages;
	}

	private async Task UpdateTeamSend(Team team) => await _dbService.EditData("UPDATE teams SET emblem = @emblem, uniformHome = @uniformHome, uniformAway = @uniformAway, deleted = @deleted, sportsid = @sportsid, name = @name WHERE id = @id", team);

	public async Task DeleteTeamValidation(int id, Guid userId)
	{
		var team = await GetByIdValidationAsync(id);
		if (team is null)
			throw new ApplicationException(Resource.TeamDoesNotExist);

		var user = await _authService.GetUserByIdAsync(userId);
		if (user is null)
			throw new ApplicationException("Usuário não existe");
		
		if (user.TeamManagementId != team.Id)
			throw new ApplicationException(Resource.UserNotAllowedToDelete);

		if (team.Deleted)
			throw new ApplicationException(Resource.TeamAlreadyDeleted);

		var championshipsId = await GetAllIdsOfChampionshipsThatTeamIsParticipatingIn(team.Id);
		foreach (var championshipId in championshipsId)
		{
			await RemoveTeamFromChampionshipValidation(team.Id, championshipId);
		}
		
		await RemoveTeamOfAllPlayerTempProfiled(team.Id);
		await RemoveTeamOfAllUsers(team.Id);
		await DeleteTeamSend(id);
		await UpdateUser(user);
		
		team.Deleted = true;
		var result = await _elasticService._client.IndexAsync(team, INDEX);

	}

	public async Task DeleteTeamValidation(int id)
	{
        Console.WriteLine("entrou");

		var team = await GetByIdValidationAsync(id);
		if (team is null)
			throw new ApplicationException(Resource.TeamDoesNotExist);

		var user = await _dbService.GetAsync<User>("SELECT * FROM Users WHERE TeamManagementId = @Id", new {id});

		if (team.Deleted)
			throw new ApplicationException(Resource.TeamAlreadyDeleted);

		var championshipsId = await GetAllIdsOfChampionshipsThatTeamIsParticipatingIn(team.Id);

		foreach (var championshipId in championshipsId)
		{
			await RemoveTeamFromChampionshipValidation(team.Id, championshipId);
		}

		await UpdateUser(user);
		await RemoveTeamOfAllPlayerTempProfiled(team.Id);
		await RemoveTeamOfAllUsers(team.Id);
		await DeleteTeamSend(id);
		await _elasticService._client.IndexAsync(team, INDEX);
	}
	private async Task RemoveTeamOfAllPlayerTempProfiled(int teamId) 
		=> await _dbService.EditData("UPDATE PlayerTempProfiles SET TeamsId = null WHERE TeamsId = @teamId", new {teamId});
	 
	private async Task RemoveTeamOfAllUsers(int teamId) 
	{
		var users = await _dbService.GetAll<User>("SELECT * FROM Users WHERE PlayerTeamId = @teamId", new {teamId});
		foreach (var user in users)
		{
			user.PlayerTeamId = 0;
			var resultado = await _elasticService._client.IndexAsync(user, INDEX2);
		}
		await _dbService.GetAll<PlayerTempProfile>("UPDATE Users SET PlayerTeamId = null WHERE PlayerTeamId = @teamId", new {teamId});
	}
		

	private async Task<List<int>> GetAllIdsOfChampionshipsThatTeamIsParticipatingIn(int teamId) 
		=> await _dbService.GetAll<int>(
			@"SELECT ChampionshipId FROM championships_teams ct
			JOIN Championships c ON ct.ChampionshipId = c.Id
			WHERE ct.TeamId = @teamId AND (c.Status = 0 OR c.Status = 3) AND c.Deleted <> true", 
			new {teamId});

	private async Task DeleteTeamSend(int id) => await _dbService.EditData("UPDATE teams SET deleted = true WHERE id = @id", new { id });

	public async Task<List<User>> GetPlayersOfTeamValidation(int id) => await GetPlayersOfteamSend(id);

	private async Task<List<User>> GetPlayersOfteamSend(int id) =>
		await _dbService.GetAll<User>(
			@"
			SELECT id, name, artisticname, number, email, teamsid, playerposition, false as iscaptain, picture, null as username, isCaptain FROM playertempprofiles WHERE teamsid = @id
			UNION ALL
			SELECT id, name, artisticname, number, email, playerteamid as teamsid, playerposition, iscaptain, picture, username, isCaptain FROM users WHERE playerteamid = @id;",
			new { id });

	private async Task<User> GetTechnicianFromTeam(int teamId) => await _dbService.GetAsync<User>("SELECT picture, name FROM users WHERE teammanagementId = @teamId", new { teamId });

	public async Task<bool> VerifyTeamHasCaptain(int teamId)
	{
		if(!await CheckIfTeamExists(teamId))
			throw new ApplicationException("Time passado não existe.");
		return await CheckIfTeamHasCaptain(teamId);
	}
	private async Task<bool> CheckIfTeamExists(int teamId)
		=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM teams WHERE Id = @teamId)", new {teamId});
	private async Task<bool> CheckIfTeamHasCaptain(int teamId)
		=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE PlayerTeamId = @teamId AND IsCaptain = true)", new {teamId});

    public static implicit operator TeamService(Lazy<TeamService> v)
    {
        throw new NotImplementedException();
    }
}
