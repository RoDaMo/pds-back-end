using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.PlayerTempProfileService;

namespace PlayOffsApi.Services;

public class PlayerTempProfileService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
	private readonly TeamService _teamService;
	private readonly JwtSettings _jwtSettings;

    public PlayerTempProfileService(DbService dbService, ElasticService elasticService, TeamService teamService,
		JwtSettings jwtSettings)
	{
		_dbService = dbService;
        _elasticService = elasticService;
		_teamService = teamService;
		_jwtSettings = jwtSettings;
	}

    public async Task<List<string>> CreateValidationAsync(PlayerTempProfile playerTempProfile, Guid userId)
	{
		var errorMessages = new List<string>();

		if(!await ChecksIfTeamExists(playerTempProfile.TeamsId))
        {
			throw new ApplicationException(Resource.CreateValidationAsyncTeamDoesntExist);
        }
		
		var team = await _teamService.GetByIdValidationAsync(playerTempProfile.TeamsId);

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

		var player = await CreateSendAsync(playerTempProfile);
		// await SendEmailToConfirmPermission(player, team);

		return errorMessages;
	}

    private async Task<PlayerTempProfile> CreateSendAsync(PlayerTempProfile playerTempProfile)
	{
		var id = await _dbService.EditData2(
			"INSERT INTO playertempprofiles (name, artisticname, number, email, teamsid, playerPosition, picture, accepted) VALUES (@Name, @ArtisticName, @Number, @Email, @TeamsId, @PlayerPosition, @Picture, true) returning id", playerTempProfile);
		return await GetTempPlayerById(id);
	}

	public async Task SendEmailToConfirmPermission(PlayerTempProfile player, Team team)
	{
		var token = GenerateJwtToken(player.Id, player.Email, DateTime.UtcNow.AddHours(48), team.Id);
		const string baseUrl = "https://www.playoffs.app.br/pages/confirmacao-entrada-time.html";
        var url = $"{baseUrl}?token={token}";
		
        var emailResponse = EmailService.SendConfirmationPermissionToJoinInTeam(player.Email, player.Name, team.Name, url);

        if(!emailResponse)
        {
            await DeletePlayerTempByIdAsync(player.Id);
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

	private async Task DeletePlayerTempByIdAsync(Guid playerId) 
		=> await _dbService.EditData("DELETE FROM playertempprofiles WHERE id = @playerId;", new {playerId});
	
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
		var player = await _dbService.GetAsync<PlayerTempProfile>("SELECT * FROM playertempprofiles WHERE Email = @email;", new { email });

        if(player == null)
        {
           throw new ApplicationException("Jogador inexistente");
        }
		if(userId != player.Id)
		{
			throw new ApplicationException("Usuário inválido");
		}
        if(player.Accepted)
        {
			throw new ApplicationException("Jogador já aceitou participar do time");
        } 

        player.Accepted = true;
        await UpdateConfirmEmailAsync(player);
	}

	private async Task UpdateConfirmEmailAsync(PlayerTempProfile player)
	{
		await _dbService.EditData("UPDATE playertempprofiles SET Accepted = @Accepted WHERE id = @Id;", player);
	}

	private async Task<bool> ChecksIfUserIsManager(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND TeamManagementId IS NULL);", new {userId});
	private async Task<bool> ChecksIfEmailAlreadyExistsInPlayerTempProfiles(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM playertempprofiles WHERE email = @email AND accepted = true);", new {email});
	private async Task<bool> ChecksIfEmailAlreadyExistsInUsers(string email) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT email FROM users WHERE email = @email AND accepted = true);", new {email});
	private async Task<bool> ChecksIfNumberAlreadyExistsInPlayerTemp(int number, int teamsId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM playertempprofiles WHERE number = @number AND teamsid = @teamsId AND accepted = true);", new {number, teamsId});
	private async Task<bool> ChecksIfNumberAlreadyExistsInUser(int number, int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT name FROM users WHERE number = @number AND PlayerTeamId = @teamId AND accepted = true);", new {number, teamId});
    private async Task<bool> ChecksIfTeamExists(int teamId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT id FROM teams WHERE id = @teamId);", new {teamId});

    public async Task<PlayerTempProfile> GetTempPlayerById(Guid id) 
	    => await _dbService.GetAsync<PlayerTempProfile>("SELECT id, name, artisticname, number, email, teamsid, playerposition, accepted, picture FROM playertempprofiles WHERE id = @id", new { id });
    
    public async Task RemoveCaptainByTeamId(int teamId) 
	    => await _dbService.EditData("UPDATE playertempprofiles SET IsCaptain = false WHERE teamsid = @teamId", new {teamId});

    public async Task MakePlayerCaptain(Guid playerId)
	    => await _dbService.EditData("UPDATE playertempprofiles SET IsCaptain = true WHERE Id = @playerId", new {playerId});
	
	public async Task DeletePlayerTempValidation(Guid id) => await DeletePlayerTempSend(id);
	private async Task DeletePlayerTempSend(Guid id) => await _dbService.EditData("DELETE FROM Reports WHERE reportedplayertempid = @id; DELETE FROM PlayerTempProfiles WHERE Id = @id;", new {id});
}
