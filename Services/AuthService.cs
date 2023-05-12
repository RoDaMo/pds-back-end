using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PlayOffsApi.Validations;

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

	public string GenerateJwtToken(Guid userId, string username)
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var claims = new[]
		{
				new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
				new Claim(JwtRegisteredClaimNames.UniqueName, username),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var expires = DateTime.UtcNow.AddHours(48);

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

	private static string EncryptPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

	private static bool VerifyEncryptedPassword(string password, string encryptedPassword) => BCrypt.Net.BCrypt.Verify(password, encryptedPassword);

	public async Task<List<string>> RegisterValidationAsync(User newUser)
	{
		var result = await new UserValidator().ValidateAsync(newUser);

		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();

		if (await UserAlreadyExists(newUser))
			return new() { "Email ou nome de usuário já cadastrado no sistema" };
		
		await RegisterUserAsync(newUser);
		return new();
	}

	public async Task<bool> UserAlreadyExists(User user)
	{
		var result = await new UserValidator().ValidateAsync(user);

		if (result.IsValid)
			return await _dbService.GetAsync<bool>("SELECT COUNT(1) FROM users WHERE username = @Username OR email = @Email", user);
		
		var errorMessages = result.Errors.Select(x => "|" + x.ErrorMessage).ToList();
		throw new ApplicationException(errorMessages.ToString());
	}

	private async Task RegisterUserAsync(User newUser)
	{
		newUser.PasswordHash = EncryptPassword(newUser.Password);
		await _dbService.EditData("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday)", newUser);
	}

	public async Task<User> VerifyCredentials(User user)
	{
		var actualUser = await _dbService.GetAsync<User>("SELECT id, passwordhash FROM users WHERE Username=@Username;", user);
		if (VerifyEncryptedPassword(user.Password, actualUser.PasswordHash))
			user.Id = actualUser.Id;

		return user;
	}
}
