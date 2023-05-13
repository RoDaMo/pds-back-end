using Microsoft.IdentityModel.Tokens;
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

	// private readonly byte[] _criptKey;
	public AuthService(string secretKey, string issuer, string audience, DbService dbService) // , byte[] criptKey
	{
		_secretKey = secretKey;
		_issuer = issuer;
		_audience = audience;
		_dbService = dbService;
		// _criptKey = criptKey;
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
		var expires = DateTime.UtcNow.AddMinutes(10);

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

		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();

		if (await UserAlreadyExists(newUser))
			return new() { "Email ou nome de usuário já cadastrado no sistema" };
		
		await RegisterUserAsync(newUser);
		return new();
	}

	public async Task<bool> UserAlreadyExists(User user)
	{
		var userValidator = new UserValidator();
		var result = await userValidator.ValidateAsync(user, options => options.IncludeRuleSets("IdentificadorUsername"));
		var result2 = await userValidator.ValidateAsync(user, options => options.IncludeRuleSets("IdentificadorEmail"));

		if (result.IsValid || result2.IsValid)
			return await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM users WHERE username = @Username OR email = @Email", user);

		throw new ApplicationException("Nome de usuário ou email inválido!");
	}

	private async Task RegisterUserAsync(User newUser)
	{
		newUser.PasswordHash = EncryptPassword(newUser.Password);
		await _dbService.EditData("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday)", newUser);
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
		=> await _dbService.GetAsync<User>("SELECT Id, Name, Username, Email, Deleted, Birthday FROM users WHERE id = @Id", new User { Id = userId});
}
