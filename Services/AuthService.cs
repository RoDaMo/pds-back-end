﻿using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PlayOffsApi.Validations;
using FluentValidation;

namespace PlayOffsApi.Services;

public class AuthService
{
	private readonly string _secretKey;
	private readonly string _issuer;
	private readonly string _audience;
	private readonly DbService _dbService;
	private readonly EmailService _emailService;
	private readonly IHttpContextAccessor _httpContextAccessor;

	// private readonly byte[] _criptKey;
	public AuthService(string secretKey, string issuer, string audience, DbService dbService, EmailService emailService, IHttpContextAccessor httpContextAccessor) // , byte[] criptKey
	{
		_secretKey = secretKey;
		_issuer = issuer;
		_audience = audience;
		_dbService = dbService;
		// _criptKey = criptKey;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
	}

	public string GenerateJwtToken(Guid userId, string email)
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var claims = new[]
		{
			new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
			new Claim(JwtRegisteredClaimNames.UniqueName, email),
			new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var expires = DateTime.UtcNow.AddHours(2);

		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new(claims),
			Expires = expires,
			Issuer = _issuer,
			Audience = _audience,
			SigningCredentials = creds
		};

		return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
	}

	// exists so that if refresh token implementation changes, it changes globaly
	public static RefreshToken GenerateRefreshToken(Guid userId) => new()
		{
			UserId = userId,
			Token = Guid.NewGuid(),
			ExpirationDate = DateTime.UtcNow.AddMonths(12)
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
			return new() { "Email ou nome de usuário já cadastrado no sistema" };

		if (await UserAlreadyExistsInPlayerTemp(newUser.Email))
		{
			var id = await CreateAccountAndAddPlayer(newUser);
			await SendEmail(id); //remove this after adding temporary player consent check functionality
			resultId.Add(id.ToString());
			return resultId;
		}		
		newUser.Id = await RegisterUserAsync(newUser);
		await SendEmail(newUser.Id);
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
			return await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM users WHERE username = @Username OR email = @Email", user);

		throw new ApplicationException("Nome de usuário ou email inválido!");
	}

	private async Task<Guid> RegisterUserAsync(User newUser)
	{
		newUser.PasswordHash = EncryptPassword(newUser.Password);
		return await _dbService.EditData2("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday, ConfirmEmail) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday, 'false') RETURNING Id;", newUser);
	}

	public async Task<User> VerifyCredentials(User user)
	{
		var actualUser = await _dbService.GetAsync<User>("SELECT id, passwordhash FROM users WHERE Username=@Username;", user);
		if (actualUser == null)
			return new();
		
		if (VerifyEncryptedPassword(user.Password, actualUser.PasswordHash))
			user.Id = actualUser.Id;

		return user;
	}

	public async Task<User> GetUserByIdAsync(Guid userId) 
		=> await _dbService.GetAsync<User>("SELECT Id, Name, Username, Email, Deleted, Birthday, cpf FROM users WHERE id = @Id", new User { Id = userId});

	public async Task<List<string>> SendEmail(Guid userId)
	{

		var user = await GetUserByIdAsync(userId);
        var errorMessages = new List<string>();
		var token = GenerateJwtToken(user.Id, user.Email);
		var httpContext = _httpContextAccessor.HttpContext;
		var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/auth/confirm-email";
        var url = $"{baseUrl}?token={token}";
    
        var emailResponse = _emailService.SendConfirmationEmail(user.Email, user.Username, url);

        if(!emailResponse)
        {
            await DeleteUserByIdAsync(userId);
            throw new ApplicationException("Não foi possível enviar o email, verifique se ele está correto ou tente novamente mais tarde.");
        }

		return errorMessages;
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
			SecurityToken securityToken;
			var principal = tokenHandler.ValidateToken(token, tokenDescriptor, out securityToken);
			jwtSecurityToken = securityToken as JwtSecurityToken;
			
		}
		catch (Exception)
		{
			throw new ApplicationException("Token de confirmação de email inválido.");;
		}

		var email = jwtSecurityToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value;
		var user = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Email = email;", email);

        if(user == null)
        {
           throw new ApplicationException("Tente se cadastrar novamente.");
        }
        if(user.ConfirmEmail)
        {
           return errorMessages;
        } 

        user.ConfirmEmail = true;
        await UpdateConfirmEmailAsync(user);

		if (user.PlayerTeamId != 0)
		{
			await DeletePlayerTempProfile(user.Id);
		}

        return errorMessages;
	}

    private async Task UpdateConfirmEmailAsync(User user)
	{
		await _dbService.EditData("UPDATE users SET ConfirmEmail = @ConfirmEmail WHERE id = @Id;", user);
	}

	private async Task<bool> UserAlreadyExistsInPlayerTemp(string email)
	{
		return await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM playertempprofiles WHERE Email = email", email);
	}

	private async Task<Guid> CreateAccountAndAddPlayer(User user)
	{
		var player = await _dbService.GetAsync<PlayerTempProfile>("SELECT * FROM playertempprofiles WHERE Email = email", user.Email);
		user.ArtisticName = player.ArtisticName;
		user.Number = player.Number;
		user.SoccerPositionId = player.SoccerPositionId;
		user.VolleyballPositionId = player.VolleyballPositionId;
		user.PlayerTeamId = player.TeamsId;
		user.PasswordHash = EncryptPassword(user.Password);

		return await _dbService.EditData2("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday, ConfirmEmail, ArtisticName, Number, SoccerPositionId, VolleyballPositionId, PlayerTeamId) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday, 'false', @ArtisticName, @Number, @SoccerPositionId, @VolleyballPositionId, @PlayerTeamId) RETURNING Id;", user);
	}

	private async Task DeletePlayerTempProfile(Guid id)
	{
		await _dbService.EditData("DELETE FROM playertempprofiles WHERE Id = id;", id);
	}
}
