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

        switch (team.SportsId)
        {
	        case 1 when team.NumberOfPlayers > 24:
		        throw new ApplicationException("Time passado já atingiu o limite de jogadores.");
	        case 2 when team.NumberOfPlayers > 14:
		        throw new ApplicationException("Time passado já atingiu o limite de jogadores.");
        }

        var playerValidator = new PlayerValidator();

		var result = await playerValidator.ValidateAsync(user);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(!await ChecksIfPositionIsValid(user.PlayerPositionsId, team.SportsId))
		{
			throw new ApplicationException("Posição inválida para o esporte do time.");
		}

		if(await ChecksIfUserIsManager(userId))
		{
			throw new ApplicationException("Apenas técnicos podem cadastrar jogadores.");
		}

        if(!await ChecksIfUserPassedExists(user.Email))
		{
			throw new ApplicationException("Usuário passado não existe.");
		}

		if(await ChecksIfNumberAlreadyExistsInPlayerTemp(user.Number, user.PlayerTeamId))
		{
			throw new ApplicationException("Já exite jogador temporário com o número de camisa passado.");
		}

        if(await ChecksIfNumberAlreadyExistsInUser(user.Number, user.PlayerTeamId))
		{
			throw new ApplicationException("Já exite jogador com o número de camisa passado.");
		}

        if(await ChecksIfTeamAlreadyHasCaptain())
		{
			throw new ApplicationException("Já exite capitão no time atual.");
		}

		if(await ChecksIfUserPassedAlreadHasTeam(user.Email))
		{
			throw new ApplicationException("Jogador passado já pertence a um time.");
		}

		await CreateSendAsync(user);
        team.NumberOfPlayers++;
		await _teamService.IncrementNumberOfPlayers(team.Id, team.NumberOfPlayers);

		return errorMessages;
	}

    private async Task CreateSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET artisticname = @ArtisticName, number = @Number, playerpositionsid = @PlayerPositionsId, iscaptain = @IsCaptain, playerteamId = @PlayerTeamId WHERE email = @Email;", user
            );
	}

    private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number, int teamsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number AND teamsid = @teamsId);", new {number, teamsId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number, int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number AND playerteamid = @teamId);", new {number, teamId});
    private async Task<bool> ChecksIfTeamAlreadyHasCaptain() => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE iscaptain = true);", new {});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});
    private async Task<bool> ChecksIfUserPassedExists(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE email = @email);", new {email});
    private async Task<bool> ChecksIfUserPassedAlreadHasTeam(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE email = @email AND playerteamid IS NOT NULL);", new {email});
	private async Task<bool> ChecksIfPositionIsValid(int playerPositionId, int sportsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM PlayerPositions WHERE id = @playerPositionId AND sportsid = @sportsId);", new {playerPositionId, sportsId});

}
