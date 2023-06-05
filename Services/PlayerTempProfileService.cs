using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.PlayerTempProfileService;

namespace PlayOffsApi.Services;

public class PlayerTempProfileService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
	private readonly TeamService _teamService;


    public PlayerTempProfileService(DbService dbService, ElasticService elasticService, TeamService teamService)
	{
		_dbService = dbService;
        _elasticService = elasticService;
		_teamService = teamService;
	}

    public async Task<List<string>> CreateValidationAsync(PlayerTempProfile playerTempProfile, Guid userId)
	{
		var errorMessages = new List<string>();

		if(!await ChecksIfTeamExists(playerTempProfile.TeamsId))
        {
			throw new ApplicationException(Resource.CreateValidationAsyncTeamDoesntExist);
        }
		
		var team = await _teamService.GetByIdSendAsync(playerTempProfile.TeamsId);

		var playerTempProfileValidator = new PlayerTempProfileValidator();

		var result = await playerTempProfileValidator.ValidateAsync(playerTempProfile);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		switch (team.SportsId)
        {
	        case 1 when ((int)playerTempProfile.PlayerPosition) > 9 || ((int)playerTempProfile.PlayerPosition) < 1 :
		        throw new ApplicationException(Resource.CreateValidationAsyncInvalidPosition);
	        case 2 when ((int)playerTempProfile.PlayerPosition) < 10 || ((int)playerTempProfile.PlayerPosition) > 14 :
		        throw new ApplicationException(Resource.CreateValidationAsyncInvalidPosition);
        }

		if(await ChecksIfUserIsManager(userId))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncOnlyTechnicians);
		}

		if(await ChecksIfEmailAlreadyExistsInPlayerTempProfiles(playerTempProfile.Email))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncPlayerAlreadyExists);
		}

		if(await ChecksIfEmailAlreadyExistsInUsers(playerTempProfile.Email))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncUserAlreadyExists);
		}

		if(await ChecksIfNumberAlreadyExistsInPlayerTemp(playerTempProfile.Number, playerTempProfile.TeamsId))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncPlayerWithNumberExists);
		}

		if(await ChecksIfNumberAlreadyExistsInUser(playerTempProfile.Number))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncExists);
		}

		await CreateSendAsync(playerTempProfile);
		return errorMessages;
	}

    private async Task CreateSendAsync(PlayerTempProfile playerTempProfile)
	{
		await _dbService.EditData(
			"INSERT INTO playertempprofiles (name, artisticname, number, email, teamsid, playerPosition) VALUES (@Name, @ArtisticName, @Number, @Email, @TeamsId, @PlayerPosition)", playerTempProfile);
	}

	private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfEmailAlreadyExistsInPlayerTempProfiles(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM playertempprofiles WHERE email = @email);", new {email});
	private async Task<bool> ChecksIfEmailAlreadyExistsInUsers(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM users WHERE email = @email);", new {email});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number, int teamsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number AND teamsid = @teamsId);", new {number, teamsId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number);", new {number});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});

}
