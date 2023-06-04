using Elastic.Clients.Elasticsearch;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Services;

public class TeamService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
    private readonly AuthService _authService;
	private const string INDEX = "teams";
	
    public TeamService(DbService dbService, ElasticService elasticService, AuthService authService)
	{
		_dbService = dbService;
        _elasticService = elasticService;
        _authService = authService;
	}

    public async Task<List<string>> CreateValidationAsync(TeamDTO teamDto, Guid userId)
	{
		var errorMessages = new List<string>();
		var teamValidator = new TeamValidator();

		var result = await teamValidator.ValidateAsync(teamDto);
		if (!await _authService.UserHasCpfValidationAsync(userId))
			throw new ApplicationException("É necessário cadastrar um CPF para criar um time.");
		
		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await IsAlreadyTechOfAnotherTeam(userId))
			throw new ApplicationException("Usuário passado já é técnico de um time.");
		
		var team = ToTeam(teamDto);

		team.Id =  await CreateSendAsync(team);
		await UpdateUser(userId, team.Id);

		var resultado = await _elasticService._client.IndexAsync(team, INDEX);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);
		
		return errorMessages;
	}


    private async Task<int> CreateSendAsync(Team team) => await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformAway, deleted, sportsid, name, numberofplayers) VALUES (@Emblem, @UniformHome, @UniformAway, @Deleted, @SportsId, @Name, 0) RETURNING Id;",
			team);

	public async Task<List<Team>> GetAllValidationAsync() => await GetAllSendAsync();

	private async Task<List<Team>> GetAllSendAsync() => await _dbService.GetAll<Team>("SELECT * FROM teams", new { });

	public async Task<Team> GetByIdValidationAsync(int id) => await GetByIdSendAsync(id);

	public async Task<Team> GetByIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id", new {id});

	private async Task<bool> IsAlreadyTechOfAnotherTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NOT NULL);", new {userId});
	public async Task IncrementNumberOfPlayers(int teamId, int numberOfPlayers)
	{
		await _dbService.EditData("UPDATE teams SET numberofplayers = @numberOfPlayers WHERE id = @teamId;", new {teamId, numberOfPlayers });
	}

	private async Task UpdateUser(Guid userId, int teamId)
	{
		await _dbService.EditData("UPDATE users SET teammanagementid = @teamId  WHERE id = @userid;", new { teamId, userId });
	}

	private static Team ToTeam(TeamDTO teamDto) => new(teamDto.Emblem, teamDto.UniformHome, teamDto.UniformAway, teamDto.SportsId, teamDto.Name);

	public async Task<List<Team>> SearchTeamsValidation(string query)
	{
		var response = await SearchTeamsSend(query);
		return response.Documents.ToList();
	}

	private async Task<SearchResponse<Team>> SearchTeamsSend(string query)
		=> await _elasticService.SearchAsync<Team>(el =>
		{
			el.Index(INDEX);
			el.Query(q => q.Bool(b => b.Must(must => must.MatchPhrasePrefix(mpp => mpp.Field(f => f.Name).Query(query)))));
		});


	public async Task AddTeamToChampionshipValidation(int teamId, int championshipId)
	{
		if (await RelationAlreadyExistsValidation(teamId, championshipId))
			throw new ApplicationException("Time já vinculado com esse campeonato");
		
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
			throw new ApplicationException("Time não vinculado com campeonato");

		await RemoveTeamFromChampionshipSend(teamId, championshipId);
	}

	private async Task RemoveTeamFromChampionshipSend(int teamId, int championshipId)
		=> await _dbService.EditData("DELETE FROM championships_teams WHERE teamId = @teamId AND championshipId = @championshipId", new { teamId, championshipId });
}
