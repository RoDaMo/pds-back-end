namespace PlayOffsApi.Models;

public class PlayerTempProfile
{
	public Guid Id { get; set; }
    public string Name { get; set; }
	public string ArtisticName { get; set; }
    public int Number { get; set; }
    public string Email { get; set; }
	public int TeamsId { get; set; }
	public bool IsCaptain { get; set; }

	public PlayerTempProfile(string name, string artisticName, int number, string email, bool isCaptain)
	{
		Name = name;
		ArtisticName = artisticName;
		Number = number;
		Email = email;
		IsCaptain = isCaptain;
	}

	public PlayerTempProfile(string name, string artisticName, int number, string email,  bool isCaptain, int teamsId)
	{
		Name = name;
		ArtisticName = artisticName;
		Number = number;
		Email = email;
		TeamsId = teamsId;
		IsCaptain = isCaptain;
	}

	public PlayerTempProfile() { }
}
