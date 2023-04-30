using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PlayOffsApi.Services;

public class AuthService
{
	private readonly string _secretKey;
	private readonly string _issuer;
	private readonly string _audience;
	private readonly DbService _dbService;
	private readonly byte[] _criptKey;
	public AuthService(string secretKey, string issuer, string audience, DbService dbService, byte[] criptKey)
	{
		_secretKey = secretKey;
		_issuer = issuer;
		_audience = audience;
		_dbService = dbService;
		_criptKey = criptKey;
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
			Subject = new ClaimsIdentity(claims),
			Expires = expires,
			Issuer = _issuer,
			Audience = _audience,
			SigningCredentials = creds
		};

		return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
	}

	private static string EncryptPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

	private static bool VerifyEncryptedPassword(string password, string encryptedPassword) => BCrypt.Net.BCrypt.Verify(password, encryptedPassword);

	public async Task RegisterUser(User newUser)
	{
		newUser.PasswordHash = EncryptPassword(newUser.Password);
		newUser.EmailHash = EncryptEmail(newUser.Email);

		await _dbService.EditData("INSERT INTO users (Name, Username, PasswordHash, EmailHash, Deleted, Birthday) VALUES (@Name, @Username, @PasswordHash, @EmailHash, @Deleted, @Birthday)", newUser);
	}

	private string EncryptEmail(string plainText)
	{
		using Aes aes = Aes.Create();
		aes.Key = _criptKey;
		aes.GenerateIV();

		using MemoryStream memoryStream = new MemoryStream();
		// Write the IV to the beginning of the MemoryStream
		memoryStream.Write(aes.IV, 0, aes.IV.Length);

		using CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
		using StreamWriter streamWriter = new StreamWriter(cryptoStream);

		streamWriter.Write(plainText);
		streamWriter.Flush();
		cryptoStream.FlushFinalBlock();

		return Convert.ToBase64String(memoryStream.ToArray());
	}

	private string DecryptEmail(string cipherText)
	{
		byte[] cipherData = Convert.FromBase64String(cipherText);

		using Aes aes = Aes.Create();
		aes.Key = _criptKey;

		using MemoryStream memoryStream = new MemoryStream(cipherData);
		byte[] iv = new byte[aes.IV.Length];
		memoryStream.Read(iv, 0, iv.Length);
		aes.IV = iv;

		using CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
		using StreamReader streamReader = new StreamReader(cryptoStream);

		return streamReader.ReadToEnd();
	}

	public async Task<User> VerifyCredentials(User user)
	{
		var actualUser = await _dbService.GetAsync<User>("SELECT id, passwordhash FROM users WHERE Username=@Username;", user);
		if (VerifyEncryptedPassword(user.Password, actualUser.PasswordHash))
			user.Id = actualUser.Id;

		return user;
	}
}
