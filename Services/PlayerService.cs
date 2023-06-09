using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.PlayerService;

namespace PlayOffsApi.Services;

public class PlayerService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
	private readonly TeamService _teamService;


    public PlayerService(DbService dbService, ElasticService elasticService, TeamService teamService)
	{
		_dbService = dbService;
        _elasticService = elasticService;
		_teamService = teamService;
	}

    public async Task<List<string>> CreateValidationAsync(User user, Guid userId)
	{
		var errorMessages = new List<string>();

        if(!await ChecksIfTeamExists(user.PlayerTeamId))
        {
			throw new ApplicationException(Resource.CreateValidationAsyncDoesntExist);
        }
		
		var team = await _teamService.GetByIdSendAsync(user.PlayerTeamId);

        var playerValidator = new PlayerValidator();

		var result = await playerValidator.ValidateAsync(user);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		switch (team.SportsId)
        {
	        case 1 when ((int)user.PlayerPosition) > 9 || ((int)user.PlayerPosition) < 1 :
		        throw new ApplicationException(Resource.CreateValidationAsyncInvalidPosition);
	        case 2 when ((int)user.PlayerPosition) < 10 || ((int)user.PlayerPosition) > 14 :
		        throw new ApplicationException(Resource.CreateValidationAsyncInvalidPosition);
        }

		if(await ChecksIfUserIsManager(userId))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncOnlyTechnicians);
		}

        if(!await ChecksIfUserPassedExists(user.Email))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncUserDoesntExist);
		}

		if(await ChecksIfNumberAlreadyExistsInPlayerTemp(user.Number, user.PlayerTeamId))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncNumberAlreadyExists);
		}

        if(await ChecksIfNumberAlreadyExistsInUser(user.Number, user.PlayerTeamId))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncAlreadyExists);
		}

        if(await ChecksIfTeamAlreadyHasCaptain())
		{
			throw new ApplicationException(Resource.CreateValidationAsyncAlreadyHasCaptain);
		}

		if(await ChecksIfUserPassedAlreadHasTeam(user.Email))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncAlreadyBelongsTeam);
		}

		await CreateSendAsync(user);
		return errorMessages;
	}

    private async Task CreateSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET artisticname = @ArtisticName, number = @Number, playerposition = @PlayerPosition, iscaptain = @IsCaptain, playerteamId = @PlayerTeamId WHERE email = @Email;", user
            );
	}

    private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number, int teamsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number AND teamsid = @teamsId);", new {number, teamsId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number, int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number AND playerteamid = @teamId);", new {number, teamId});
    private async Task<bool> ChecksIfTeamAlreadyHasCaptain() => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE iscaptain = true);", new {});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});
    private async Task<bool> ChecksIfUserPassedExists(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE email = @email);", new {email});
    private async Task<bool> ChecksIfUserPassedAlreadHasTeam(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE email = @email AND playerteamid IS NOT NULL);", new {email});

}
