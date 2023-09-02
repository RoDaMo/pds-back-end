using System.Text.Json.Serialization;
using PlayOffsApi.Enum;
using PlayOffsApi.Middleware;

namespace PlayOffsApi.Models;

public class User
{
	public User()
	{
		Username = string.Empty;
		Email = string.Empty;
	}
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Email { get; set; }
	public string Password { get; set; }
	public string Username { get; set; }
	public string PasswordHash { get; set; }
	public bool Deleted { get; set; }
	public DateTime Birthday { get; set; }
	public string Cpf { get; set; }
	public int TeamManagementId  { get; set; }
	public int PlayerTeamId  { get; set; }
	public string ArtisticName { get; set; }
	public int Number { get; set; }
	public bool IsCaptain { get; set; }
	public string Bio { get; set; }
	public string Picture { get; set; }
	public bool RememberMe { get; set; }
	public bool ConfirmEmail{ get; set; }
	public PlayerPosition PlayerPosition { get; set; }
	public int ChampionshipId { get; set; }
	public string Role { get; set; } = "user";
	
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string CaptchaToken { get; set; }

	public bool IsOrganizer { get; set; }
	public string Cnpj { get; set; }
}
