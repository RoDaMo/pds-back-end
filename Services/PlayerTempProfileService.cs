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

		// var teamValidator = new TeamValidator();

		// var result = teamValidator.Validate(teamDto);

		// if (!result.IsValid)
		// {
		// 	errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
		// 	return errorMessages;
		// }

		//verificar se é um técnico
		//Verificar se já existe usuário com esse email (temporário ou não)
		//Verificar tamanho do time

		await CreateSendAsync(playerTempProfile);

		return errorMessages;
	}

	public async Task CreateSendAsync(PlayerTempProfile playerTempProfile)
	{
		
		await _dbService.EditData(
			"INSERT INTO playertempprofiles (name, artisticname, number, email, teamsid) VALUES (@Name, @ArtisticName, @Number, @Email, @TeamsId)", playerTempProfile);
	}

}
