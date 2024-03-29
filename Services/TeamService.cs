using Elastic.Clients.Elasticsearch;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.TeamService;
using Generic = PlayOffsApi.Resources.Generic;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using ServiceStack;

namespace PlayOffsApi.Services;

public class TeamService
{
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
    private readonly AuthService _authService;
    private readonly ChampionshipService _championshipService;
	private readonly string _index;
	private readonly string _userIndex;
	private readonly BracketingMatchService _bracketingMatchService;
	private readonly JwtSettings _jwtSettings;
	
    public TeamService(DbService dbService, ElasticService elasticService, AuthService authService,
	 	ChampionshipService championshipService, BracketingMatchService bracketingMatchService, JwtSettings jwtSettings)
	{
		_dbService = dbService;
        _elasticService = elasticService;
        _authService = authService;
        _championshipService = championshipService;
		_bracketingMatchService = bracketingMatchService;
		var isDevelopment = Environment.GetEnvironmentVariable("IS_DEVELOPMENT");
		_index = isDevelopment == "false" ? "teams" : "teams-dev";
		_userIndex = isDevelopment == "false" ? "users" : "users-dev";
		_jwtSettings = jwtSettings;
	}

    public async Task<List<string>> CreateValidationAsync(TeamDTO teamDto, Guid userId)
	{
		var errorMessages = new List<string>();
		var teamValidator = new TeamValidator();

		var result = await teamValidator.ValidateAsync(teamDto);
		if (!await _authService.UserHasCpfValidationAsync(userId) && !await _authService.UserHasCnpjValidationAsync(userId))
			throw new ApplicationException(Resource.CreateValidationAsyncCpfNeeded);
		
		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await IsAlreadyTechOfAnotherTeam(userId))
			throw new ApplicationException(Resource.CreateValidationAsyncAlreadyCoach);
		
		var team = ToTeam(teamDto);

		team.Id =  await CreateSendAsync(team);
		await UpdateUser(userId, team.Id);

		var resultado = await _elasticService._client.IndexAsync(team, _index);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);
		
		return errorMessages;
	}


    private async Task<int> CreateSendAsync(Team team) => await _dbService.EditData(
			"INSERT INTO teams (emblem, uniformHome, uniformAway, deleted, sportsid, name) VALUES (@Emblem, @UniformHome, @UniformAway, @Deleted, @SportsId, @Name) RETURNING Id;",
			team);

	public async Task<List<Team>> GetAllValidationAsync(Sports sport) => await GetAllSendAsync(sport);

	private async Task<List<Team>> GetAllSendAsync(Sports sport) => await _dbService.GetAll<Team>("SELECT * FROM teams WHERE sportId = @sport AND Deleted <> true", new { sport });

	public async Task<Team> GetByIdValidationAsync(int id, bool getDeletedTeam = false)
	{
		var team = await GetByIdSendAsync(id, getDeletedTeam);
		if (team is null)
			return null;
		
		team.Technician = await GetTechnicianFromTeam(team.Id);
		return team;
	}

	public async Task<Team> GetByIdSendAsync(int id, bool getDeletedTeam) => await _dbService.GetAsync<Team>($"SELECT * FROM teams where id=@id {(getDeletedTeam ? "" : "AND deleted = false")}", new {id});

	private async Task<bool> IsAlreadyTechOfAnotherTeam(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT TeamManagementId FROM users WHERE Id = @userId AND (TeamManagementId IS NOT NULL OR TeamManagementId <> 0));", new {userId});

	private async Task UpdateUser(User user)
	{
		user.TeamManagementId = 0;
		var resultado = await _elasticService._client.IndexAsync(user, _userIndex);
		await _dbService.EditData("UPDATE users SET teammanagementid = null  WHERE id = @userid;", new { userId = user.Id });
	}
	private async Task UpdateUser(Guid userId, int teamId)
	{
		var user = await _authService.GetUserByIdAsync(userId);
		user.TeamManagementId = teamId;
		var resultado = await _elasticService._client.IndexAsync(user, _userIndex);
		await _dbService.EditData("UPDATE users SET teammanagementid = @teamId  WHERE id = @userid;", new { userId, teamId });
	}

	private static Team ToTeam(TeamDTO teamDto) => new(teamDto.Emblem, teamDto.UniformHome, teamDto.UniformAway, teamDto.SportsId, teamDto.Name);

	public async Task<List<Team>> SearchTeamsValidation(string query, Sports sport, int championshipId)
	{
		var response = await SearchTeamsSend(query, sport);
		var linkedTeams = await _championshipService.GetAllTeamsLinkedToValidation(championshipId);
		// var hashSet = linkedTeams.ToHashSet();
		
		var responseList = response.Documents.ToList();
		// responseList.RemoveAll(r => hashSet.Contains(r.Id));
		return responseList;
	}

	private async Task<SearchResponse<Team>> SearchTeamsSend(string query, Sports sports)
		=> await _elasticService.SearchAsync<Team>(el =>
		{
			el.Index(_index);
			el.Query(q => q.Bool(b => b.
					Must(
						must => must.MatchPhrasePrefix(mpp => mpp.Field(f => f.Name).Query(query)),
						must2 => must2.Term(t => t.Field(f => f.Deleted).Value(false)),
						must3 => must3.Term(t => t.Field(f => f.SportsId).Value((int)sports))
					)
				)
			);
		});


	public async Task AddTeamToChampionshipValidation(int teamId, int championshipId)
	{
		if (await RelationAlreadyExistsValidation(teamId, championshipId))
			throw new ApplicationException(Resource.AddTeamToChampionshipValidationTeamAlreadyLinked);

		if (!await _championshipService.CanMoreTeamsBeAddedValidation(championshipId))
			throw new ApplicationException("O limite de times para esse campeonato já foi atingido");
		
		if(await CheckIfTeamWereDeleted(teamId))
			throw new ApplicationException("Time indisponível");
			
		await AddTeamToChampionshipSend(teamId, championshipId);

		// var championship = await _championshipService.GetByIdValidation(championshipId);
		// var user = await GetTechnicianFromTeam(teamId);
		// await SendEmailToConfirmPermission(user, championship);
	}

	public async Task SendEmailToConfirmPermission(User user, Championship championship)
	{
		var token = GenerateJwtToken(user.Id, user.Email, DateTime.UtcNow.AddHours(48), championship.Id);
		const string baseUrl = "https://www.playoffs.app.br/pages/confirmacao-entrada-time.html";
        var url = $"{baseUrl}?token={token}";
		
        var emailResponse = EmailService.SendConfirmationPermissionToJoinInChampionship(user.Email, user.Username, championship.Name, url);

        if(!emailResponse)
        {
			await _dbService.EditData("DELETE FROM championships_teams WHERE TeamId = @teamId, ChampionshipId = @championshipId", new {teamId = user.TeamManagementId, championshipId = championship.Id } );
            throw new ApplicationException("Não foi possível enviar o email. Tente novamente mais tarde.");
        }
	}

	public string GenerateJwtToken(Guid userId, string email, DateTime expirationDate, int championshipId)
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
			new Claim(JwtRegisteredClaimNames.Email, email),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new Claim(JwtRegisteredClaimNames.UniqueName, championshipId.ToString())
		};

		var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtSettings.Key));
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
			var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtSettings.Key));

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
		var championshipId = jwtSecurityToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value.ToInt();

        if(user == null || championshipId == 0)
        {
           throw new ApplicationException("Não foi possível confirmar participação no campeonato");
        }
		if(userId != user.Id)
		{
			throw new ApplicationException("Usuário incorreto");
		}
        if(await _dbService.GetAsync<bool>("SELECT Accepted FROM championships_teams WHERE TeamId = @teamId AND ChampionshipId = @championshipId", new {teamId = user.TeamManagementId, championshipId} ))
        {
			throw new ApplicationException("Participação já confirmada");
        } 
        await UpdateConfirmEmailAsync(championshipId, user.TeamManagementId);
	}

	private async Task UpdateConfirmEmailAsync(int championshipId, int teamId)
	{
		await _dbService.EditData("UPDATE championships_teams SET Accepted = true WHERE TeamId = @teamId AND ChampionshipId = @championshipId;",  new {teamId, championshipId});
	}
	private async Task<bool> CheckIfTeamWereDeleted(int teamId)
		=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Teams WHERE id = @teamId AND Deleted = true)",
		 new {teamId});

	private async Task AddTeamToChampionshipSend(int teamId, int championshipId)
	{
		await _dbService.EditData(
			"INSERT INTO championships_teams (teamId, championshipId, Accepted) VALUES (@teamId, @championshipId, true)",
			new { teamId, championshipId });
	}

	public async Task<bool> RelationAlreadyExistsValidation(int teamId, int championshipId)
		=> await RelationAlreadyExistsSend(teamId, championshipId);

	private async Task<bool> RelationAlreadyExistsSend(int teamId, int championshipId)
		=> await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM championships_teams WHERE teamId = @teamId AND championshipId = @championshipId AND accepted = true", new { teamId, championshipId });

	public async Task RemoveTeamFromChampionshipValidation(int teamId, int championshipId)
	{
		if (!await RelationAlreadyExistsValidation(teamId, championshipId))
			throw new ApplicationException(Resource.RemoveTeamFromChampionshipValidationTeamNotLinked);
		
		var championship = await _championshipService.GetByIdValidation(championshipId);

		if(await BracketingExists(championshipId) && championship.Status is Enum.ChampionshipStatus.Active or Enum.ChampionshipStatus.Pendent && championship.Deleted == false)
		{
			var matches = await _dbService.GetAll<Match>(
				@"SELECT * FROM Matches WHERE (Visitor = @teamId OR Home = @teamId) AND ChampionshipId = @championshipId AND Winner IS NULL AND Tied <> true", new { teamId, championshipId });

			foreach (var match in matches.Where(match => match.Winner == 0 && !match.Tied))
			{
				await _bracketingMatchService.WoValidation(match.Id, match.Visitor == teamId ? match.Home : match.Visitor);
			}
		}

		await RemoveTeamFromChampionshipSend(teamId, championshipId);
	}
	public async Task<bool> BracketingExists(int championshipId)
	=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Matches WHERE championshipId = @championshipId)", new {championshipId});

	private async Task RemoveTeamFromChampionshipSend(int teamId, int championshipId)
		=> await _dbService.EditData("DELETE FROM championships_teams WHERE teamId = @teamId AND championshipId = @championshipId", new { teamId, championshipId });

	public async Task<List<Championship>> GetChampionshipsOfTeamValidation(int id) => await GetChampionshipsOfTeamSend(id);

	private async Task<List<Championship>> GetChampionshipsOfTeamSend(int id)
		=> await _dbService.GetAll<Championship>("SELECT c.id, c.name, c.logo, c.description, c.format, c.sportsid FROM championships c JOIN championships_teams ct ON c.id = ct.championshipid WHERE ct.teamid = @id AND c.Deleted = false AND ct.Accepted = true", new { id });

	public async Task<List<string>> UpdateTeamValidation(TeamDTO teamDto, Guid userId)
	{
		var errorMessages = new List<string>();
		var teamValidator = new TeamValidator();
		
		var result = await teamValidator.ValidateAsync(teamDto);
		var user = await _authService.GetUserByIdAsync(userId);
		if (!user.TeamManagementId.Equals(teamDto.Id))
			throw new ApplicationException(Resource.UserNotAllowedToUpdate);
		
		
		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		var team = ToTeam(teamDto);
		team.Id = teamDto.Id;
		await UpdateTeamSend(team);
		
		var resultado = await _elasticService._client.IndexAsync(team, _index);
		if (!resultado.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);
		
		return errorMessages;
	}

	private async Task UpdateTeamSend(Team team) => await _dbService.EditData("UPDATE teams SET emblem = @emblem, uniformHome = @uniformHome, uniformAway = @uniformAway, deleted = @deleted, sportsid = @sportsid, name = @name WHERE id = @id", team);

	public async Task DeleteTeamValidation(int id, Guid userId)
	{
		var team = await GetByIdValidationAsync(id);
		if (team is null)
			throw new ApplicationException(Resource.TeamDoesNotExist);

		var user = await _authService.GetUserByIdAsync(userId);
		if (user is null)
			throw new ApplicationException("Usuário não existe");
		
		if (user.TeamManagementId != team.Id)
			throw new ApplicationException(Resource.UserNotAllowedToDelete);

		if (team.Deleted)
			throw new ApplicationException(Resource.TeamAlreadyDeleted);

		var championshipsId = await GetAllIdsOfChampionshipsThatTeamIsParticipatingIn(team.Id);
		foreach (var championshipId in championshipsId)
		{
			await RemoveTeamFromChampionshipValidation(team.Id, championshipId);
		}
		
		await RemoveTeamOfAllPlayerTempProfiled(team.Id);
		await RemoveTeamOfAllUsers(team.Id);
		await DeleteTeamSend(id);
		await UpdateUser(user);
		
		team.Deleted = true;
		var result = await _elasticService._client.IndexAsync(team, _index);

	}

	public async Task DeleteTeamValidation(int id)
	{
		var team = await GetByIdValidationAsync(id);
		if (team is null)
			throw new ApplicationException(Resource.TeamDoesNotExist);

		var user = await _dbService.GetAsync<User>("SELECT * FROM Users WHERE TeamManagementId = @Id", new {id});

		if (team.Deleted)
			throw new ApplicationException(Resource.TeamAlreadyDeleted);

		var championshipsId = await GetAllIdsOfChampionshipsThatTeamIsParticipatingIn(team.Id);

		foreach (var championshipId in championshipsId)
		{
			await RemoveTeamFromChampionshipValidation(team.Id, championshipId);
		}

		await UpdateUser(user);
		await RemoveTeamOfAllPlayerTempProfiled(team.Id);
		await RemoveTeamOfAllUsers(team.Id);
		
		await DeleteTeamSend(id);
		await _elasticService._client.IndexAsync(team, _index);
	}
	private async Task RemoveTeamOfAllPlayerTempProfiled(int teamId) 
		=> await _dbService.EditData("UPDATE PlayerTempProfiles SET TeamsId = null WHERE TeamsId = @teamId", new {teamId});
	 
	private async Task RemoveTeamOfAllUsers(int teamId) 
	{
		var users = await _dbService.GetAll<User>("SELECT * FROM Users WHERE PlayerTeamId = @teamId", new {teamId});
		foreach (var user in users)
		{
			user.PlayerTeamId = 0;
			var resultado = await _elasticService._client.IndexAsync(user, _userIndex);
		}
		await _dbService.GetAll<PlayerTempProfile>("UPDATE Users SET PlayerTeamId = null WHERE PlayerTeamId = @teamId", new {teamId});
	}
		

	private async Task<List<int>> GetAllIdsOfChampionshipsThatTeamIsParticipatingIn(int teamId) 
		=> await _dbService.GetAll<int>(
			@"SELECT ChampionshipId FROM championships_teams ct
			JOIN Championships c ON ct.ChampionshipId = c.Id
			WHERE ct.TeamId = @teamId AND (c.Status = 0 OR c.Status = 3) AND c.Deleted <> true AND ct.Accepted = true", 
			new {teamId});

	private async Task DeleteTeamSend(int id) => await _dbService.EditData("UPDATE teams SET deleted = true WHERE id = @id", new { id });

	public async Task<List<User>> GetPlayersOfTeamValidation(int id) => await GetPlayersOfteamSend(id);

	private async Task<List<User>> GetPlayersOfteamSend(int id) =>
		await _dbService.GetAll<User>(
			@"
			SELECT id, name, artisticname, number, email, teamsid, playerposition, false as iscaptain, picture, null as username, isCaptain FROM playertempprofiles WHERE teamsid = @id AND accepted = true
			UNION ALL
			SELECT id, name, artisticname, number, email, playerteamid as teamsid, playerposition, iscaptain, picture, username, isCaptain FROM users WHERE playerteamid = @id AND accepted = true;",
			new { id });

	private async Task<User> GetTechnicianFromTeam(int teamId) => await _dbService.GetAsync<User>("SELECT id, picture, name, email, username FROM users WHERE teammanagementId = @teamId ", new { teamId });

	public async Task<bool> VerifyTeamHasCaptain(int teamId)
	{
		if(!await CheckIfTeamExists(teamId))
			throw new ApplicationException("Time passado não existe.");
		return await CheckIfTeamHasCaptain(teamId);
	}
	private async Task<bool> CheckIfTeamExists(int teamId)
		=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM teams WHERE Id = @teamId)", new {teamId});
	private async Task<bool> CheckIfTeamHasCaptain(int teamId)
		=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE PlayerTeamId = @teamId AND IsCaptain = true)", new {teamId});

	public async Task IndexAllTeamsValidation()
	{
		var teams = await _dbService.GetAll<Team>("SELECT id, emblem, uniformhome, uniformaway, deleted, sportsid, name FROM teams", new {});
		foreach (var team in teams)
			await _elasticService._client.IndexAsync(team, _index);
	}
}
