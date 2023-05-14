using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;

public class TeamService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
	private const string INDEX = "teams";


    public TeamService(DbService dbService, ElasticService elasticService)
	{
		_dbService = dbService;
        _elasticService = elasticService;
	}

    public async Task<List<string>> CreateValidationAsync(TeamDTO teamDto, Guid userId)
	{
		var errorMessages = new List<string>();

		var teamValidator = new TeamValidator();

		var result = teamValidator.Validate(teamDto);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await IsAlreadyTechOfAnotherTeam(userId))
		{
			throw new ApplicationException("Usuário passado já é técnico de um time.");
		}

		var team = ToTeam(teamDto);

		var teamId =  await CreateSendAsync(team);
		await UpdateUser(teamDto.Cpf, userId, teamId);

		return errorMessages;
	}

	public async Task<int> CreateSendAsync(Team team) => await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformWay, deleted, sportsid, name, numberofplayers) VALUES (@Emblem, @UniformHome, @UniformWay, @Deleted, @SportsId, @Name, 0) RETURNING Id;",
			team);

	public async Task<List<Team>> GetAllValidationAsync() => await GetAllSendAsync();

	public async Task<List<Team>> GetAllSendAsync() => await _dbService.GetAll<Team>("SELECT * FROM teams", new { });

	public async Task<Team> GetByIdValidationAsync(int id) => await GetByIdSendAsync(id);

	public async Task<Team> GetByIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id", new {id});

	public async Task<bool> IsAlreadyTechOfAnotherTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NOT NULL);", new {userId});
	public async Task IncrementNumberOfPlayers(int teamId, int numberOfPlayers)
	{
		await _dbService.EditData("UPDATE teams SET numberofplayers = @numberOfPlayers WHERE id = @teamId;", new {teamId, numberOfPlayers });
	}

	private async Task UpdateUser(string cpf, Guid userId, int teamId)
	{
		await _dbService.EditData("UPDATE users SET cpf = @cpf, teammanagementid = @teamId  WHERE id = @userid;", new {cpf,teamId, userId });
	}

	private Team ToTeam(TeamDTO teamDTO) => new Team(teamDTO.Emblem, teamDTO.UniformHome, teamDTO.UniformWay, teamDTO.SportsId, teamDTO.Name);
}
