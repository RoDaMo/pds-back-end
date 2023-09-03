using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Elastic.Clients.Elasticsearch;
using PlayOffsApi.Validations;
using FluentValidation;
using PlayOffsApi.DTO;
using Resource = PlayOffsApi.Resources.Services.AuthService;
using Generic = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Services;

public class AuthService
{
	private readonly string _secretKey;
	private readonly string _issuer;
	private readonly string _audience;
	private readonly DbService _dbService;
	private readonly ElasticService _elastic;
	private const string Index = "users";
	private const string INDEX = "championships";

	public AuthService(string secretKey, string issuer, string audience, DbService dbService, ElasticService elastic) 
	{
		_secretKey = secretKey;
		_issuer = issuer;
		_audience = audience;
		_dbService = dbService;
		_elastic = elastic;
	}

	public string GenerateJwtToken(Guid userId, string email, DateTime expirationDate, string role = "user")
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
			new Claim(JwtRegisteredClaimNames.UniqueName, email),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new Claim(ClaimTypes.Role, role)
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new(claims),
			Expires = expirationDate,
			Issuer = _issuer,
			Audience = _audience,
			SigningCredentials = creds
		};

		return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
	}

	// exists so that if refresh token implementation changes, it changes globaly
	public static RefreshToken GenerateRefreshToken(Guid userId, DateTime expiration) => new()
		{
			UserId = userId,
			Token = Guid.NewGuid(),
			ExpirationDate = expiration
		};

	private static string EncryptPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

	private static bool VerifyEncryptedPassword(string password, string encryptedPassword) => BCrypt.Net.BCrypt.Verify(password, encryptedPassword);

	public async Task<List<string>> RegisterValidationAsync(User newUser)
	{
		var result = await new UserValidator().ValidateAsync(newUser, options => options.IncludeRuleSets("IdentificadorUsername", "IdentificadorEmail", "Dados"));
		var resultId = new List<string>();

		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();

		if (await UserAlreadyExists(newUser))
			return new() { Resource.UserAlreadyRegistered };

		newUser.Picture = "https://playoffs-api.up.railway.app/img/e82930b9-b71c-442a-9bc9-95b189c19afb";

		if (await UserAlreadyExistsInPlayerTemp(newUser.Email))
		{
			var id = await CreateAccountAndAddPlayer(newUser);
			await SendEmailToConfirmAccount(id); //TODO: remove this after adding temporary player consent check functionality
			resultId.Add(id.ToString());
			return resultId;
		}

		newUser.Id = await RegisterUserAsync(newUser);
		
		if (newUser.Role != "admin")
			await SendEmailToConfirmAccount(newUser.Id);
		else
			newUser.ConfirmEmail = true;
		
		await _elastic._client.IndexAsync(newUser, Index);
		resultId.Add(newUser.Id.ToString());

		return resultId;
	}

	private async Task DeleteUserByIdAsync(Guid userId) 
		=> await _dbService.GetAsync<User>("DELETE FROM users WHERE id = @userId;", new {userId});

	public async Task<bool> UserAlreadyExists(User user)
	{
		var userValidator = new UserValidator();
		var result = await userValidator.ValidateAsync(user, options => options.IncludeRuleSets("IdentificadorUsername"));
		var result2 = await userValidator.ValidateAsync(user, options => options.IncludeRuleSets("IdentificadorEmail"));

		if (result.IsValid || result2.IsValid)
			return await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM users WHERE (username = @Username OR email = @Email) AND Deleted = false ", user);

		throw new ApplicationException(Resource.InvalidUsername);
	}

	private async Task<Guid> RegisterUserAsync(User newUser)
	{
		newUser.PasswordHash = EncryptPassword(newUser.Password);
		return await _dbService.EditData2("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday, ConfirmEmail, picture, role) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday, @ConfirmEmail, @picture, @role) RETURNING Id;", newUser);
	}

	public async Task<User> VerifyCredentials(User user)
	{
		var actualUser = await _dbService.GetAsync<User>("SELECT id, passwordhash, ConfirmEmail, Role FROM users WHERE Username=@Username AND deleted = false;", user);
		if (actualUser == null)
			return new();

		if (!VerifyEncryptedPassword(user.Password, actualUser.PasswordHash)) return user;
		
		user.Id = actualUser.Id;
		user.ConfirmEmail = actualUser.ConfirmEmail;
		user.Role = actualUser.Role;
		return user;
	}

	public async Task<User> GetUserByIdAsync(Guid userId) 
		=> await _dbService.GetAsync<User>(
			@"
			SELECT id, name, artisticname, number, email, playerteamid, playerposition, iscaptain, picture, username, championshipid, teammanagementid FROM users WHERE id = @Id AND deleted = false
			UNION ALL
			SELECT  id, name, artisticname, number, email, teamsid as playerteamid, playerposition, iscaptain, picture, null as username, null as championshipId, null as teammanagementid FROM playertempprofiles WHERE id = @Id", 
			new User { Id = userId });

	public async Task SendEmailToConfirmAccount(Guid userId)
	{

		var user = await GetUserByIdAsync(userId);
		var token = GenerateJwtToken(user.Id, user.Email, DateTime.UtcNow.AddHours(2));
		const string baseUrl = "https://www.playoffs.app.br/pages/confirmacao-cadastro.html";
        var url = $"{baseUrl}?token={token}";
		
        var emailResponse = EmailService.SendConfirmationEmail(user.Email, user.Username, url);

        if(!emailResponse)
        {
            await DeleteUserByIdAsync(userId);
            throw new ApplicationException(Resource.ErrorSendingConfirmationEmail);
        }

	}
	
	public async Task<List<string>> ConfirmEmail(string token)
	{
        var errorMessages = new List<string>();
		var jwtSecurityToken = new JwtSecurityToken();

		try
		{
			var tokenHandler = new JwtSecurityTokenHandler();
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

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
			var email2 = jwtSecurityToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value;
			var user2 = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Email = @email2;", new { email2 });
			errorMessages.Add(user2.Id.ToString());
			errorMessages.Add(Resource.InvalidEmailToken);
			return errorMessages;
		}

		var email = jwtSecurityToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value;
		var user = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Email = @email;", new { email });

        if(user == null)
        {
           throw new ApplicationException(Resource.TryAgain);
        }
        if(user.ConfirmEmail)
        {
			errorMessages.Add(user.Username);
        	return errorMessages;
        } 

        user.ConfirmEmail = true;
        await UpdateConfirmEmailAsync(user);
        await _elastic._client.IndexAsync(user, Index);

		if (user.PlayerTeamId != 0)
		{
			await DeletePlayerTempProfile(user.Id);
		}

		errorMessages.Add(user.Username);
        return errorMessages;
	}

    private async Task UpdateConfirmEmailAsync(User user)
	{
		await _dbService.EditData("UPDATE users SET ConfirmEmail = @ConfirmEmail WHERE id = @Id;", user);
	}

	private async Task<bool> UserAlreadyExistsInPlayerTemp(string email)
	{
		return await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM playertempprofiles WHERE Email = @email)", new {email});
	}

	private async Task<Guid> CreateAccountAndAddPlayer(User user)
	{
		var player = await _dbService.GetAsync<PlayerTempProfile>("SELECT * FROM playertempprofiles WHERE Email = @email", user);
		user.ArtisticName = player.ArtisticName;
		user.Number = player.Number;
		user.PlayerPosition = player.PlayerPosition;
		user.PlayerTeamId = player.TeamsId;
		user.PasswordHash = EncryptPassword(user.Password);
		
		var guidPlayer = await _dbService.EditData2("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday, ConfirmEmail, ArtisticName, Number, PlayerPosition, PlayerTeamId) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday, 'false', @ArtisticName, @Number, @PlayerPositionsId, @PlayerTeamId) RETURNING Id;", user);
		await _dbService.EditData("DELETE FROM playertempprofiles WHERE id = @id", new { id = player.Id });

		return guidPlayer;
	}

	private async Task DeletePlayerTempProfile(Guid id)
	{
		await _dbService.EditData("DELETE FROM playertempprofiles WHERE Id = @id;", id);
	}

	public async Task<List<string>> ForgotPassword(User user)
	{
		var errorMessages = new List<string>();
		var result = await new UserValidator().ValidateAsync(user, options => options.IncludeRuleSets("IdentificadorEmail"));

		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();
		
		var actualUser = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Email = @Email;", new { Email = user.Email });

		if(actualUser is null)
			throw new ApplicationException(Resource.ForgotPasswordInvalidEmail);

		if (!actualUser.ConfirmEmail)
				throw new ApplicationException(Resource.ConfirmEmailToAccess);
		
		await SendEmailToResetPassword(actualUser.Id);
		errorMessages.Add(actualUser.Id.ToString());
		return errorMessages;
	}

	public async Task SendEmailToResetPassword(Guid userId)
	{

		var user = await GetUserByIdAsync(userId);
		var token = GenerateJwtToken(user.Id, user.Email, DateTime.UtcNow.AddHours(2));
		const string baseUrl = "https://www.playoffs.app.br/pages/redefinir-senha.html";
        var url = $"{baseUrl}?token={token}";
    
        var emailResponse = EmailService.SendEmailPasswordReset(user.Email, user.Username, url);

        if (!emailResponse)
        {
            throw new ApplicationException(Resource.ErrorSendingConfirmationEmail);
        }

	}

	public List<string> ConfirmResetPassword(string token)
	{
		var errorMessages = new List<string>();
		var jwtSecurityToken = new JwtSecurityToken();
		var tokenHandler = new JwtSecurityTokenHandler();
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

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
		
		var email = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value;
		errorMessages.Add(email);
		return errorMessages;
	}

	public async Task<List<string>> ResetPassword(User user)
	{
		var errorMessages = new List<string>();
		var result = await new UserValidator().ValidateAsync(user, options => options.IncludeRuleSets("Password"));

		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();
		
		var actualUser = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Email = @Email;", new { Email = user.Email });
		
		actualUser.PasswordHash = EncryptPassword(user.Password);
		
		await UpdatePasswordSendAsync(actualUser);
		await _elastic._client.IndexAsync(actualUser, Index);

		return errorMessages;
	}

	private async Task UpdatePasswordSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET passwordhash = @PasswordHash WHERE id = @Id;", user
            );
	}
	public async Task<List<string>> UpdateProfileValidationAsync(User user, Guid userId)
	{
		var errorMessages = new List<string>();
		var userValidator = new UserValidator();
		var actualUser = await GetUserByIdAsync(userId);
		var ruleSets = new List<string>();

		if (!string.IsNullOrEmpty(user.Username))
		{
			ruleSets.Add("IdentificadorUsername");
			ruleSets.Add("Bio");
			actualUser.Username = user.Username;
			actualUser.Bio = user.Bio;
		}

		if (!string.IsNullOrEmpty(user.Email))
		{
			ruleSets.Add("IdentificadorEmail");
			actualUser.Email = user.Email;
		}

		if (!string.IsNullOrEmpty(user.Name))
		{
			ruleSets.Add("Name");
			actualUser.Name = user.Name;
		}

		actualUser.ArtisticName = user.ArtisticName ?? actualUser.ArtisticName;
		actualUser.Picture = user.Picture ?? actualUser.Picture;
		
		var result = await userValidator.ValidateAsync(user, options => options.IncludeRuleSets(ruleSets.ToArray()));

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if (await CheckIfUserIsPlayerAsync(userId) && !string.IsNullOrEmpty(user.ArtisticName))
			throw new ApplicationException(Resource.NotPlayer);
		
		user.Id = userId;

        if (await OtherUserAlreadyExists(user))
	        throw new ApplicationException(Resource.InvalidUsername);
		

		await UpdateProfileSendAsync(actualUser);
		await _elastic._client.IndexAsync(actualUser, Index);

		return errorMessages;
	}

    private async Task UpdateProfileSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET name = @Name, username = @UserName, email = @Email, artisticname = @ArtisticName, bio = @Bio, picture = @Picture WHERE id = @Id;", user
        );
	}

    public async Task<List<string>> UpdatePasswordValidationAsync(UpdatePasswordDTO updatePasswordDTO, Guid userId)
	{
		var errorMessages = new List<string>();

		var result = await new UpdatePasswordValidator().ValidateAsync(updatePasswordDTO);

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

        var actualUser = await _dbService.GetAsync<User>("SELECT id, passwordhash FROM users WHERE id = @userId;", new {userId});

        if (!VerifyEncryptedPassword(updatePasswordDTO.CurrentPassword, actualUser.PasswordHash))
        {
            throw new ApplicationException(Resource.UpdatePasswordValidationAsyncInvalidPassword);
        }

		actualUser.PasswordHash = EncryptPassword(updatePasswordDTO.NewPassword);

		await UpdatePasswordSendAsync(actualUser);
		await _elastic._client.IndexAsync(actualUser, Index);

		return errorMessages;
	}

	private async Task<bool> CheckIfUserIsPlayerAsync(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT playerteamid FROM users WHERE id = @userId AND playerteamid is null);", new {userId});

	private async Task<bool> OtherUserAlreadyExists(User user) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE id <> @Id AND (username = @Username OR email = @Email))", user);

	public async Task<bool> UserHasCpfValidationAsync(Guid userId) => await UserHasCpfSendAsync(userId);

	private async Task<bool> UserHasCpfSendAsync(Guid userId) =>
		await _dbService.GetAsync<bool>("SELECT CASE WHEN COALESCE(TRIM(cpf), '') = '' THEN false ELSE true END FROM users WHERE id = @userId", new { userId });

	public async Task<bool> CpfAlreadyExistsValidation(string cpf) => await CpfAlreadyExistsSend(cpf);

	private async Task<bool> CpfAlreadyExistsSend(string cpf)
		=> await _dbService.GetAsync<bool>("SELECT COUNT(cpf) FROM users WHERE cpf = @cpf", new { cpf });
	
	public async Task<List<string>> AddCpfUserValidationAsync(Guid userId, string cpf)
	{
		if (await UserHasCpfValidationAsync(userId)) throw new ApplicationException(Resource.AddCpfUserValidationAsyncHasCpf);
		if (await CpfAlreadyExistsValidation(cpf)) throw new ApplicationException(Resource.AddCpfUserValidationAsyncCpfCadastrado);
		var userValidator = new UserValidator();
		var results = await userValidator.ValidateAsync(new User { Cpf = cpf }, option => option.IncludeRuleSets("Cpf"));
		
		if (!results.IsValid) return results.Errors.Select(x => x.ErrorMessage).ToList();

		var numberCpf = new int[11];
		for (var i = 0; i < 11; i++)
			numberCpf[i] = int.Parse(cpf[i].ToString());
		
		var sum = 0;
		for (var i = 0; i < 9; i++)
			sum += numberCpf[i] * (10 - i);
		

		var firstVerifierDigit = (sum * 10) % 11;
		firstVerifierDigit = firstVerifierDigit == 10 ? 0 : firstVerifierDigit;
		
		sum = 0;
		var arrayNova = numberCpf;
		arrayNova[9] = firstVerifierDigit; 
		for (var i = 0; i < 10; i++)
			sum += arrayNova[i] * (11 - i);
		
		var secondVerifierDigit = (sum * 10) % 11;
		secondVerifierDigit = secondVerifierDigit == 10 ? 0 : secondVerifierDigit;

		if (firstVerifierDigit != numberCpf[9] || secondVerifierDigit != numberCpf[10]) throw new ApplicationException(Resource.InvalidCpf);

		await AddCpfUserSend(new() { Id = userId, Cpf = cpf });
		return new();
	}

	private async Task AddCpfUserSend(User user) 
		=> await _dbService.EditData("UPDATE users SET cpf = @cpf WHERE id = @id", user);

	public async Task<User> DeleteCurrentUserValidation(Guid userId)
	{
		var user = await GetUserByIdAsync(userId);

		if (user is null)
			throw new ApplicationException("Esse usuário não existe");
		
		if(user.ChampionshipId != 0)
		{
			var championship = await _dbService.GetAsync<Championship>("SELECT * FROM Championships WHERE Id = @id", new {id = user.ChampionshipId});
			await DeleteValidation(championship);
		}

		await DeleteCurrentUserSend(userId);
		return user;
	}
	public async Task DeleteValidation(Championship championship)
	{
		await DeleteSend(championship);
		championship.Deleted = true;
		await _elastic._client.IndexAsync(championship, INDEX);
	}

	private async Task DeleteSend(Championship championship)
	{
		await _dbService.EditData("UPDATE championships SET deleted = true WHERE id = @id", championship);
		await _dbService.EditData("UPDATE users SET championshipid = null WHERE id = @organizerId", championship);
	}

	private async Task DeleteCurrentUserSend(Guid userId) =>
		await _dbService.EditData("UPDATE users SET deleted = true WHERE id = @userId", new { userId });
	
	public async Task IndexAllUsersValidation()
	{
		var users = await GetAllUsersForIndexingSend();
		foreach (var user in users)
		{
			
			await _elastic._client.IndexAsync(user, Index);
		}
	}

	private async Task<List<User>> GetAllUsersForIndexingSend() => await _dbService.GetAll<User>("SELECT * FROM users", new {});

	public async Task<List<User>> GetUsersByUsernameValidation(string username, bool filtrarSuborganizadores = false)
	{
		var searchResponse = await GetUsersByUsernameSend(username, filtrarSuborganizadores);

		if (!searchResponse.IsValidResponse)
			throw new ApplicationException(Generic.GenericErrorMessage);

		return searchResponse.Documents.ToList();
	}
	
	private async Task<SearchResponse<User>> GetUsersByUsernameSend(string username, bool filtrarSuborganizadores) => 
		await _elastic.SearchAsync<User>(el => 
			el.Query(q =>
				q.Bool(b => 
					b.Must(
						m => m.MatchPhrasePrefix(mpp => mpp.Field(f => f.Name).Query(username)), 
						m2 => m2.Term(t => t.Field(f => f.ChampionshipId).Value(0)), 
						m3 => m3.Term(t => t.Field(f => f.PlayerTeamId).Value(0)),
						m4 => m4.Term(t => t.Field(f => f.TeamManagementId).Value(0)),
						m5 => m5.Term(t => t.Field(f => f.Deleted).Value(false)),
						m6 => m6.Term(t => t.Field(f => f.ConfirmEmail).Value(true)),
						m7 =>
						{
							if (!filtrarSuborganizadores) m7.Term(t => t.Field(f => f.IsCaptain).Value(false));
							m7.Term(t => t.Field(f => f.IsOrganizer).Value(false));
						})
				)
			).Index(Index)
		);
}
