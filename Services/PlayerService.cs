using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.PlayerService;

namespace PlayOffsApi.Services;

public class PlayerService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
	private readonly TeamService _teamService;
	private readonly PlayerTempProfileService _playerTempProfileService;
	private readonly string _index;
	private readonly JwtSettings _jwtSettings;

    public PlayerService(DbService dbService, ElasticService elasticService, TeamService teamService,
		PlayerTempProfileService playerTempProfileService, JwtSettings jwtSettings)
	{
		_dbService = dbService;
        _elasticService = elasticService;
		_teamService = teamService;
		_playerTempProfileService = playerTempProfileService;
		var isDevelopment = Environment.GetEnvironmentVariable("IS_DEVELOPMENT");
		_index = string.IsNullOrEmpty(isDevelopment) || isDevelopment == "false" ? "users" : "users-dev";
		_jwtSettings = jwtSettings;
	}

    public async Task<List<string>> CreateValidationAsync(User user, Guid userId)
	{
		var errorMessages = new List<string>();

        if (!await ChecksIfTeamExists(user.PlayerTeamId))
	        throw new ApplicationException(Resource.CreateValidationAsyncDoesntExist);
        
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
			throw new ApplicationException(Resource.CreateValidationAsyncOnlyTechnicians);
		

        if(!await ChecksIfUserPassedExists(user.Id) && user.Accepted)
	        throw new ApplicationException(Resource.CreateValidationAsyncUserDoesntExist);

        if(await ChecksIfNumberAlreadyExistsInPlayerTemp(user.Number, user.PlayerTeamId))
	        throw new ApplicationException(Resource.CreateValidationAsyncNumberAlreadyExists);

        if(await ChecksIfNumberAlreadyExistsInUser(user.Number, user.PlayerTeamId))
	        throw new ApplicationException(Resource.CreateValidationAsyncAlreadyExists);

        if(await ChecksIfUserPassedAlreadHasTeam(user.Id))
	        throw new ApplicationException(Resource.CreateValidationAsyncAlreadyBelongsTeam);

        await CreateSendAsync(user);
        user = await GetUserByIdAsync(user.Id);
		await SendEmailToConfirmPermission(user, team);
        await _elasticService._client.IndexAsync(user, _index);
		return errorMessages;
	}

    private async Task CreateSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET artisticname = @ArtisticName, number = @Number, playerposition = @PlayerPosition, iscaptain = @IsCaptain, playerteamId = @PlayerTeamId WHERE id = @Id;", user
            );
	}

	public async Task SendEmailToConfirmPermission(User user, Team team)
	{
		var token = GenerateJwtToken(user.Id, user.Email, DateTime.UtcNow.AddHours(48), team.Id);
		const string baseUrl = "https://www.playoffs.app.br/pages/confirmacao-entrada-time.html";
        var url = $"{baseUrl}?token={token}";
		
        var emailResponse = EmailService.SendConfirmationPermissionToJoinInTeam(user.Email, user.Username, team.Name, url);

        if(!emailResponse)
        {
            throw new ApplicationException("Não foi possível enviar o email. Tente novamente mais tarde.");
        }
	}

	public string GenerateJwtToken(Guid userId, string email, DateTime expirationDate, int teamId)
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
			new Claim(JwtRegisteredClaimNames.Email, email),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new Claim(JwtRegisteredClaimNames.UniqueName, teamId.ToString())
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new(claims),
			Expires = expirationDate,
			Issuer = _jwtSettings.Issuer,
			Audience = _jwtSettings.Audience,
			SigningCredentials = creds
		};

		return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
	}
	
	public async Task ConfirmEmail(string token, Guid userId)
	{
		var jwtSecurityToken = new JwtSecurityToken();

		try
		{
			var tokenHandler = new JwtSecurityTokenHandler();
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));

			var tokenDescriptor = new TokenValidationParameters
			{
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = key,
				ValidateIssuer = false,
				ValidateAudience = false,
				ClockSkew = TimeSpan.Zero
			};
			tokenHandler.ValidateToken(token, tokenDescriptor, out var securityToken);
			jwtSecurityToken = securityToken as JwtSecurityToken;
			
		}
		catch (Exception)
		{
			throw new ApplicationException("Token inválido");
		}

		var email = jwtSecurityToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
		var user = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Email = @email;", new { email });

        if(user == null)
        {
           throw new ApplicationException("Jogador inexistente");
        }
		if(userId != user.Id)
		{
			throw new ApplicationException("Usuário inválido");
		}
        if(user.Accepted)
        {
			throw new ApplicationException("Jogador já aceitou participar do time");
        } 

        user.Accepted = true;
        await UpdateConfirmEmailAsync(user);
        await _elasticService._client.IndexAsync(user, _index);
	}

	private async Task UpdateConfirmEmailAsync(User user)
	{
		await _dbService.EditData("UPDATE users SET Accepted = @Accepted WHERE id = @Id;", user);
	}

    private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
    private async Task<bool> ChecksIfUserIsManager(Guid userId, int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId = @teamId);", new { userId, teamId });
    private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number, int teamsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number AND teamsid = @teamsId AND accepted = true);", new {number, teamsId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number, int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number AND playerteamid = @teamId AND accepted = true);", new {number, teamId});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});
    private async Task<bool> ChecksIfUserPassedExists(Guid id) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE id = @id);", new {id});
    private async Task<bool> ChecksIfUserPassedAlreadHasTeam(Guid id) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE id = @id AND playerteamid IS NOT NULL AND accepted = true);", new {id});

    public async Task RemovePlayerFromTeamValidation(int teamId, Guid id, Guid organizerId)
    {
	    if (!await ChecksIfUserIsManager(organizerId, teamId))
		    throw new ApplicationException(Resource.UserNotAllowedRemovePlayer);
	    
	    var team = await _teamService.GetByIdValidationAsync(teamId);
	    if (team is null)
		    throw new ApplicationException(Resource.TeamDoesntExist);
	    
	    var (isInTeam, playerTemp) = await UserIsInTeam(teamId, id);
	    if (!isInTeam)
		    throw new ApplicationException("Usuário não pertence a este time.");

	    if (playerTemp is not null)
	    {
		    await RemovePlayerTempFromTeamSend(teamId, id);
		    return;
	    }
	    
	    await RemovePlayerFromTeamSend(teamId, id);
	    var user = await GetUserByIdAsync(id);
	    await _elasticService._client.IndexAsync(user, _index);
    }

    private async Task RemovePlayerFromTeamSend(int userTeamManagementId, Guid id) =>
	    await _dbService.EditData($"UPDATE users SET playerteamid = null WHERE Id = @Id AND playerteamid = @teamId", new { teamId = userTeamManagementId, id });
    private async Task RemovePlayerTempFromTeamSend(int userTeamManagementId, Guid id) =>
	    await _dbService.EditData($"DELETE FROM playertempprofiles WHERE Id = @Id AND teamsid = @teamId", new { teamId = userTeamManagementId, id });
    private async Task<(bool, PlayerTempProfile)> UserIsInTeam(int teamId, Guid id)
    {
	    var playerUser = await _dbService.GetAsync<User>("SELECT Id FROM users WHERE Id = @id AND playerteamid = @teamId AND accepted = true", new { id, teamId });
	    var playerExists = playerUser is not null;

	    var playerTemp = await _dbService.GetAsync<PlayerTempProfile>("SELECT Id FROM playertempprofiles WHERE Id = @id AND teamsid = @teamId AND accepted = true", new { id, teamId });
	    playerExists = playerExists || playerTemp is not null;

	    return (playerExists, playerTemp);
    }

	public async Task<List<string>> UpdateCaptainValidationAsync(Guid playerId, Guid managerId)
	{
		if (await ChecksIfUserIsManager(managerId))
			throw new ApplicationException(Resource.CreateValidationAsyncOnlyTechnicians);

		var player = await GetUserByIdAsync(playerId);

		if (player is null)
		{
			var tempPlayer = await _playerTempProfileService.GetTempPlayerById(playerId);
			if (tempPlayer is null)
				throw new ApplicationException("Jogador não existe");

			if (tempPlayer.IsCaptain)
			{
				await RemoveCaptainByTeamId(tempPlayer.TeamsId);
				return new();
			}
			await RemoveCaptainByTeamId(tempPlayer.TeamsId);
			await _playerTempProfileService.MakePlayerCaptain(tempPlayer.Id);
			return new();
		}
		
		if (player.IsCaptain)
			await RemoveCaptainByTeamId(player.PlayerTeamId);
		else
		{
			await RemoveCaptainByTeamId(player.PlayerTeamId);
			await MakePlayerCaptain(player.Id);
		}

		return new();
	}

	private async Task<User> GetUserByIdAsync(Guid userId) 
		=> await _dbService.GetAsync<User>("SELECT id, name, username, email, passwordhash, deleted, birthday, cpf, teammanagementid, artisticname, number, playerteamid, iscaptain, picture, championshipid, bio, confirmemail, role, playerposition, cnpj FROM users WHERE id = @Id AND deleted = false", new User { Id = userId });
	private async Task RemoveCaptainByTeamId(int teamId)
	{
		await _dbService.EditData(
			@"UPDATE users SET IsCaptain = false WHERE PlayerTeamId = @teamId AND accepted = true; 
			UPDATE playertempprofiles SET IsCaptain = false WHERE TeamsId = @teamId AND accepted = true;", 
			new {teamId});
	}
	private async Task MakePlayerCaptain(Guid playerId)
		=> await _dbService.EditData("UPDATE users SET IsCaptain = true WHERE Id = @playerId AND accepted = true", new {playerId});

	
}
