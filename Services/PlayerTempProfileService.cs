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
		playerTempProfile.Picture ??= "https://playoffs-api.up.railway.app/img/e82930b9-b71c-442a-9bc9-95b189c19afb";
		
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

		if(await ChecksIfNumberAlreadyExistsInUser(playerTempProfile.Number, playerTempProfile.TeamsId))
		{
			throw new ApplicationException(Resource.CreateValidationAsyncExists);
		}

		await CreateSendAsync(playerTempProfile);
		return errorMessages;
	}

    private async Task CreateSendAsync(PlayerTempProfile playerTempProfile)
	{
		await _dbService.EditData(
			"INSERT INTO playertempprofiles (name, artisticname, number, email, teamsid, playerPosition, picture) VALUES (@Name, @ArtisticName, @Number, @Email, @TeamsId, @PlayerPosition, @Picture)", playerTempProfile);
	}

	private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfEmailAlreadyExistsInPlayerTempProfiles(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM playertempprofiles WHERE email = @email);", new {email});
	private async Task<bool> ChecksIfEmailAlreadyExistsInUsers(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM users WHERE email = @email);", new {email});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number, int teamsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number AND teamsid = @teamsId);", new {number, teamsId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number, int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number AND PlayerTeamId = @teamId);", new {number, teamId});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});

    public async Task<PlayerTempProfile> GetTempPlayerById(Guid id) 
	    => await _dbService.GetAsync<PlayerTempProfile>("SELECT id, name, artisticname, number, email, teamsid, playerposition, picture FROM playertempprofiles WHERE id = @id", new { id });
    
    public async Task RemoveCaptainByTeamId(int teamId) 
	    => await _dbService.EditData("UPDATE playertempprofiles SET IsCaptain = false WHERE teamsid = @teamId", new {teamId});

    public async Task MakePlayerCaptain(Guid playerId)
	    => await _dbService.EditData("UPDATE playertempprofiles SET IsCaptain = true WHERE Id = @playerId", new {playerId});
	
	public async Task DeletePlayerTempValidation(Guid id) => await DeletePlayerTempSend(id);
	private async Task DeletePlayerTempSend(Guid id) => await _dbService.EditData("DELETE FROM PlayerTempProfiles WHERE Id = @id", new {id});
}
