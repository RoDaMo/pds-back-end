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
	public int SoccerPositionId { get; set; }
	public int VolleyballPositionId { get; set; }
	public string ArtisticName { get; set; }
    public int Number { get; set; }
	public bool IsCaptain { get; set; }
	public bool RememberMe { get; set; }
}
