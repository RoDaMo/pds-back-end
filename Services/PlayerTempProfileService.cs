using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;

public class PlayerTempProfileService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;


    public PlayerTempProfileService(DbService dbService, ElasticService elasticService)
	{
		_dbService = dbService;
        _elasticService = elasticService;
	}

    public async Task<List<string>> CreateValidationAsync(PlayerTempProfile playerTempProfile, Guid userId)
	{
		var errorMessages = new List<string>();

		var playerTempProfileValidator = new PlayerTempProfileValidator();

		var result = playerTempProfileValidator.Validate(playerTempProfile);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(!(await ChecksIfUserIsManager(userId)))
		{
			throw new ApplicationException("Apenas técnicos podem cadastrar jogadores temporários.");
		}

		if(await ChecksIfEmailAlreadyExistsInPlayerTempProfiles(playerTempProfile.Email))
		{
			throw new ApplicationException("Já exite jogador temporário com o email passado.");
		}

		if(await ChecksIfEmailAlreadyExistsInUsers(playerTempProfile.Email))
		{
			throw new ApplicationException("Já exite usuário com o email passado.");
		}

		await CreateSendAsync(playerTempProfile);

		return errorMessages;
	}

	public async Task CreateSendAsync(PlayerTempProfile playerTempProfile)
	{
		
		await _dbService.EditData(
			"INSERT INTO playertempprofiles (name, artisticname, number, email, teamsid) VALUES (@Name, @ArtisticName, @Number, @Email, @TeamsId)", playerTempProfile);
	}

	private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT ManagersId FROM teams WHERE ManagersId = @userId);", new {userId});

	private async Task<bool> ChecksIfEmailAlreadyExistsInPlayerTempProfiles(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM playertempprofiles WHERE email = @email);", new {email});
	private async Task<bool> ChecksIfEmailAlreadyExistsInUsers(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT emailhash FROM users WHERE emailhash = @email);", new {email});



}
