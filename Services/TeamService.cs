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

		var teamId =  await CreateSendAsync(team);
		await UpdateUser(userId, teamId);

		var resultado = await _elasticService._client.IndexAsync(team, INDEX);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);
		
		return errorMessages;
	}


    private async Task<int> CreateSendAsync(Team team) => await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformAway, deleted, sportsid, name) VALUES (@Emblem, @UniformHome, @UniformAway, @Deleted, @SportsId, @Name) RETURNING Id;",
			team);

	public async Task<List<Team>> GetAllValidationAsync() => await GetAllSendAsync();

	private async Task<List<Team>> GetAllSendAsync() => await _dbService.GetAll<Team>("SELECT * FROM teams", new { });

	public async Task<Team> GetByIdValidationAsync(int id) => await GetByIdSendAsync(id);

	public async Task<Team> GetByIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id", new {id});

	private async Task<bool> IsAlreadyTechOfAnotherTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NOT NULL);", new {userId});

	private async Task UpdateUser(Guid userId, int teamId)
	{
		await _dbService.EditData("UPDATE users SET teammanagementid = @teamId  WHERE id = @userid;", new { teamId, userId });
	}

	private static Team ToTeam(TeamDTO teamDto) => new(teamDto.Emblem, teamDto.UniformHome, teamDto.UniformAway, teamDto.SportsId, teamDto.Name);
}
