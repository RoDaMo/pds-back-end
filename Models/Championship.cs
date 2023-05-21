using System.Text.Json.Serialization;
using Elastic.Clients.Elasticsearch;
using PlayOffsApi.CustomJsonConverters;
using PlayOffsApi.Enum;

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
	public string Nation { get; set; }
	public string State { get; set; }
	public string City { get; set; }
	public string Neighborhood { get; set; }
}
