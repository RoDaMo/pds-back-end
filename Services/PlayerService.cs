using FluentValidation;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

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
			throw new ApplicationException("Time passado não existe.");
        }
		
		var team = await _teamService.GetByIdSendAsync(user.PlayerTeamId);

        if(team.SportsId == 1 && team.NumberOfPlayers > 24)
		{
			throw new ApplicationException("Time passado já atingiu o limite de jogadores.");
		}

		if(team.SportsId == 2 && team.NumberOfPlayers > 14)
		{
			throw new ApplicationException("Time passado já atingiu o limite de jogadores.");
		}

		var PlayerValidator = new PlayerValidator();

		var result = (team.SportsId == 1) 
		? PlayerValidator.Validate(user, options => options.IncludeRuleSets("ValidationSoccer"))
		: PlayerValidator.Validate(user, options => options.IncludeRuleSets("ValidationVolleyBall"));

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await ChecksIfUserIsManager(userId))
		{
			throw new ApplicationException("Apenas técnicos podem cadastrar jogadores.");
		}

        if(!await ChecksIfUserPassedExists(user.Id))
		{
			throw new ApplicationException("Usuário passado não existe.");
		}

		if(await ChecksIfNumberAlreadyExistsInPlayerTemp(user.Number))
		{
			throw new ApplicationException("Já exite jogador temporário com o número de camisa passado.");
		}

        if(await ChecksIfNumberAlreadyExistsInUser(user.Number))
		{
			throw new ApplicationException("Já exite jogador com o número de camisa passado.");
		}

        if(await ChecksIfTeamAlreadyHasCaptain())
		{
			throw new ApplicationException("Já exite capitão no time atual.");
		}

		if(await ChecksIfUserPassedAlreadHasTeam(user.Id))
		{
			throw new ApplicationException("Jogador passado já pertence a um time.");
		}

		await CreateSendAsync(user);
        team.NumberOfPlayers++;
		await _teamService.IncrementNumberOfPlayers(team.Id, team.NumberOfPlayers);

		return errorMessages;
	}

	public async Task CreateSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET artisticname = @ArtisticName, number = @Number, soccerpositionid = @SoccerPositionId, volleyballpositionid = @VolleyballPositionId, iscaptain = @IsCaptain, playerteamId = @PlayerTeamId WHERE id = @Id;", user
            );
	}

    private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number);", new {number});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number);", new {number});
    private async Task<bool> ChecksIfTeamAlreadyHasCaptain() => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE iscaptain = true);", new {});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});
    private async Task<bool> ChecksIfUserPassedExists(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE id = @userId);", new {userId});
    private async Task<bool> ChecksIfUserPassedAlreadHasTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE id = @userId AND playerteamid IS NOT NULL);", new {userId});

}
