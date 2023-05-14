using FluentValidation;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

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
			throw new ApplicationException("Time passado não existe.");
        }
		
		var team = await _teamService.GetByIdSendAsync(playerTempProfile.TeamsId);

		if(team.SportsId == 1 && team.NumberOfPlayers > 24)
		{
			throw new ApplicationException("Time passado já atingiu o limite de jogadores.");
		}

		if(team.SportsId == 2 && team.NumberOfPlayers > 14)
		{
			throw new ApplicationException("Time passado já atingiu o limite de jogadores.");
		}

		var playerTempProfileValidator = new PlayerTempProfileValidator();

		var result = (team.SportsId == 1) 
		? playerTempProfileValidator.Validate(playerTempProfile, options => options.IncludeRuleSets("ValidationSoccer"))
		: playerTempProfileValidator.Validate(playerTempProfile, options => options.IncludeRuleSets("ValidationVolleyBall"));

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await ChecksIfUserIsManager(userId))
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

		if(await ChecksIfNumberAlreadyExistsInPlayerTemp(playerTempProfile.Number))
		{
			throw new ApplicationException("Já exite jogador temporário com o número de camisa passado.");
		}

		if(await ChecksIfNumberAlreadyExistsInUser(playerTempProfile.Number))
		{
			throw new ApplicationException("Já exite jogador com o número de camisa passado.");
		}

		await CreateSendAsync(playerTempProfile);
		team.NumberOfPlayers++;
		await _teamService.IncrementNumberOfPlayers(team.Id, team.NumberOfPlayers);

		return errorMessages;
	}

	public async Task CreateSendAsync(PlayerTempProfile playerTempProfile)
	{
		await _dbService.EditData(
			"INSERT INTO playertempprofiles (name, artisticname, number, email, teamsid, soccerpositionid, volleyballpositionid) VALUES (@Name, @ArtisticName, @Number, @Email, @TeamsId, @SoccerPositionId, @VolleyballPositionId)", playerTempProfile);
	}

	private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfEmailAlreadyExistsInPlayerTempProfiles(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM playertempprofiles WHERE email = @email);", new {email});
	private async Task<bool> ChecksIfEmailAlreadyExistsInUsers(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT emailhash FROM users WHERE emailhash = @email);", new {email});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number);", new {number});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number);", new {number});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});

}
