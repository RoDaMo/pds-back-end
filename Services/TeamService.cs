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

    public async Task<List<string>> CreateValidationAsync(TeamDTO teamDto)
	{
		var errorMessages = new List<string>();

		var teamValidator = new TeamValidator();

		var result = teamValidator.Validate(teamDto);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await IsAlreadyTechOfAnotherTeam(teamDto.ManagersId))
		{
			throw new ApplicationException("Usuário passado já é técnico de um time.");
		}

		var team = ToTeam(teamDto);

		await CreateSendAsync(team);
		await UpdateUser(teamDto.Cpf, teamDto.ManagersId);

		return errorMessages;
	}

	public async Task CreateSendAsync(Team team)
	{
		var teamId = await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformWay, deleted, sportsid, name, managersId) VALUES (@Emblem, @UniformHome, @UniformWay, @Deleted, @SportsId, @Name, @ManagersId) RETURNING Id;",
			team);
	}

	public async Task<List<Team>> GetAllValidationAsync() => await GetAllSendAsync();

	public async Task<List<Team>> GetAllSendAsync() => await _dbService.GetAll<Team>("SELECT * FROM teams", new { });

	public async Task<Team> GetByIdValidationAsync(int id) => await GetByIdSendAsync(id);

	public async Task<Team> GetByIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id", new {id});

	public async Task<bool> IsAlreadyTechOfAnotherTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT ManagersId FROM teams WHERE ManagersId = @userId);", new {userId});

	private async Task UpdateUser(string cpf, Guid userId)
	{
		await _dbService.EditData("UPDATE users SET Cpf = @cpf WHERE id = @userid;", new {cpf, userId});
	}

	private Team ToTeam(TeamDTO teamDTO) => new Team(teamDTO.Emblem, teamDTO.UniformHome, teamDTO.UniformWay, teamDTO.SportsId, teamDTO.Name, teamDTO.ManagersId);
	
}
