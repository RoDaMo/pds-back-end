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

    public async Task<List<string>> CreateValidationAsync(Team team)
	{
		var errorMessages = new List<string>();

		var teamValidator = new TeamValidator();

		var result = await teamValidator.ValidateAsync(team);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		await CreateSendAsync(team);

		return errorMessages;
	}

    private async Task CreateSendAsync(Team team)
	{
		team.Id = await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformWay, deleted, sportsid, name) VALUES (@Emblem, @UniformHome, @UniformWay, @Deleted, @SportsId, @Name) RETURNING Id;",
			team);
	}

	public async Task<List<Team>> GetAllValidationAsync() => await GetAllSendAsync();

	private async Task<List<Team>> GetAllSendAsync() => await _dbService.GetAll<Team>("SELECT * FROM teams", new { });

	public async Task<Team> GetByIdValidationAsync(int id) => await GetByIdSendAsync(id);

	private async Task<Team> GetByIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id", new {id});
}
