using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using PlayOffsApi.Enum;
using PlayOffsApi.Middleware;

namespace PlayOffsApi.Models;

public class Championship
{
	public Championship(string name, DateTime initialDate, DateTime finalDate, int teamQuantity, Format format)
	{
		Name = name;
		InitialDate = initialDate;
		FinalDate = finalDate;
		TeamQuantity = teamQuantity;
		Format = format;
	}
	public Championship() { }
	
	public int Id { get; set; }
	public string Name { get; set; }
	public DateTime InitialDate { get; set; }
	public DateTime FinalDate { get; set; }
	[JsonConverter(typeof(SportsAsNumberConverter))]
	public Sports SportsId { get; set; }
	public string PitId { get; set; }
	public IReadOnlyCollection<FieldValue> Sort { get; set; }
	public int TeamQuantity { get; set; }
	public string Rules { get; set; }
	public string Logo { get; set; }
	public string Description { get; set; }
	[JsonConverter(typeof(FormatAsNumberConverter))]
	public Format Format { get; set; }
	public Guid OrganizerId { get; set; }
	public User Organizer { get; set; }
	public bool Deleted { get; set; }
	public List<Team> Teams { get; set; }
	
	[JsonConverter(typeof(StatusAsNumberConverter))]
	public ChampionshipStatus Status { get; set; }
	public bool DoubleMatchGroupStage { get; set; }
	public bool DoubleMatchEliminations { get; set; }
	public bool DoubleStartLeagueSystem { get; set; }
	public bool FinalDoubleMatch { get; set; }
}
