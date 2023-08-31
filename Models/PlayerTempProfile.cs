using System.Text.Json.Serialization;
using PlayOffsApi.Enum;
using PlayOffsApi.Middleware;

namespace PlayOffsApi.Models;

public class PlayerTempProfile
{
	public Guid Id { get; set; }
    public string Name { get; set; }
	public string ArtisticName { get; set; }
	public string Picture { get; set; }
    public int Number { get; set; }
    public string Email { get; set; }
	public int TeamsId { get; set; }
	public PlayerPosition PlayerPosition { get; set; }
	public bool IsCaptain { get; set; }

	public PlayerTempProfile(string name, string artisticName, int number, string email, string picture)
	{
		Name = name;
		ArtisticName = artisticName;
		Number = number;
		Email = email;
		Picture = picture;
	}

	public PlayerTempProfile(string name, string artisticName, int number, string email, int teamsId, string picture)
	{
		Name = name;
		ArtisticName = artisticName;
		Number = number;
		Email = email;
		TeamsId = teamsId;
		Picture = picture;
	}

	public PlayerTempProfile() { }
}
