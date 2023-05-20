using PlayOffsApi.Enum;

namespace PlayOffsApi.Models;

public class PlayerTempProfile
{
	public Guid Id { get; set; }
    public string Name { get; set; }
	public string ArtisticName { get; set; }
    public int Number { get; set; }
    public string Email { get; set; }
	public int TeamsId { get; set; }
	public int SoccerPositionId { get; set; }
	public int VolleyballPositionId { get; set; }

	public PlayerTempProfile(string name, string artisticName, int number, string email)
	{
		Name = name;
		ArtisticName = artisticName;
		Number = number;
		Email = email;
	}

	public PlayerTempProfile(string name, string artisticName, int number, string email, int teamsId)
	{
		Name = name;
		ArtisticName = artisticName;
		Number = number;
		Email = email;
		TeamsId = teamsId;
	}

	public PlayerTempProfile() { }
}
