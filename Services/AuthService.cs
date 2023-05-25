using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PlayOffsApi.Validations;
using FluentValidation;
using PlayOffsApi.DTO;

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
		var expires = DateTime.UtcNow.AddDays(1);

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
		
		newUser.Picture = "https://cdn-icons-png.flaticon.com/512/17/17004.png";
		
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
		await _dbService.EditData("INSERT INTO users (Name, Username, PasswordHash, Email, Deleted, Birthday, Picture) VALUES (@Name, @Username, @PasswordHash, @Email, @Deleted, @Birthday, @Picture)", newUser);
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

	public async Task<List<string>> UpdateProfileValidationAsync(User user, Guid userId)
	{
		var errorMessages = new List<string>();

		var userValidator = new UserValidator();

		var result = await new UserValidator().ValidateAsync(user, options => options.IncludeRuleSets("IdentificadorUsername", "IdentificadorEmail", "Update"));

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

		if(await checkIfUserIsPlayerAsync(userId) && user.ArtisticName is not null)
		{
			throw new ApplicationException("Apenas jogadores podem alterar o nome artístico");
		}

		user.Id = userId;

        if(await OtherUserAlreadyExists(user))
		{
			throw new ApplicationException("Nome de usuário ou email inválido!");
		}

		await UpdateProfileSendAsync(user);

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
            throw new ApplicationException("Senha atual incorreta");
        }

		actualUser.Password = EncryptPassword(updatePasswordDTO.NewPassword);

		await UpdatePasswordSendAsync(actualUser);

		return errorMessages;
	}

    private async Task UpdatePasswordSendAsync(User user)
	{
		await _dbService.EditData(
            "UPDATE users SET passwordhash = @Password WHERE id = @Id;", user
            );
	}

	private async Task<bool> checkIfUserIsPlayerAsync(Guid userId) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT playerteamid FROM users WHERE id = @userId AND playerteamid is null);", new {userId});

	private async Task<bool> OtherUserAlreadyExists(User user) => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE id <> @Id AND (username = @Username OR email = @Email))", user);

	public async Task<bool> UserHasCpfValidationAsync(Guid userId) => await UserHasCpfSendAsync(userId);

	private async Task<bool> UserHasCpfSendAsync(Guid userId) =>
		await _dbService.GetAsync<bool>("SELECT CASE WHEN COALESCE(TRIM(cpf), '') = '' THEN false ELSE true END FROM users WHERE id = @userId", new { userId });

	public async Task<List<string>> AddCpfUserValidationAsync(Guid userId, string cpf)
	{
		if (await UserHasCpfValidationAsync(userId)) throw new ApplicationException("Usuário já possui CPF");
		
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

		if (firstVerifierDigit != numberCpf[9] || secondVerifierDigit != numberCpf[10]) throw new ApplicationException("CPF inválido");

		await AddCpfUserSend(new() { Id = userId, Cpf = cpf });
		return new();
	}

	private async Task AddCpfUserSend(User user) 
		=> await _dbService.EditData("UPDATE users SET cpf = @cpf WHERE id = @id", user);
}
