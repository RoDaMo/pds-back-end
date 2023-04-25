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

		var result = teamValidator.Validate(team);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		await CreateSendAsync(team);

		return errorMessages;
	}

	public async Task CreateSendAsync(Team team)
	{
		team.Id = await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformWay, deleted, sportsid, name) VALUES (@Emblem, @UniformHome, @UniformWay, @Deleted, @SportsId, @Name) RETURNING Id;",
			team);
	}

	
}
