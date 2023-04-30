using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PlayOffsApi.Services;

public class AuthService
{
	private readonly string _secretKey;
	private readonly string _issuer;
	private readonly string _audience;
	public AuthService(string secretKey, string issuer, string audience)
	{
		_secretKey = secretKey;
		_issuer = issuer;
		_audience = audience;
	}

	public string GenerateJwtToken(string userId, string email)
	{
		var tokenHandler = new JwtSecurityTokenHandler();

		var claims = new[]
		{
				new Claim(JwtRegisteredClaimNames.Sub, userId),
				new Claim(JwtRegisteredClaimNames.UniqueName, email),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
		};

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var expires = DateTime.UtcNow.AddMinutes(60);

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
}
