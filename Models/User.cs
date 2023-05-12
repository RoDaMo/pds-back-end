namespace PlayOffsApi.Models;

public class User
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Email { get; set; }
	public string Password { get; set; }
	public string Username { get; set; }
	public string PasswordHash { get; set; }
	public bool Deleted { get; set; }
	public DateTime Birthday { get; set; }
}
