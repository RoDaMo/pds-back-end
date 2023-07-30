using PlayOffsApi.Enum;

namespace PlayOffsApi.Models;

public class ChampionshipActivityLog
{
    public int Id { get; set; }
    public DateTime DateOfActivity { get; set; }
    public TypeOfActivity TypeOfActivity { get; set; }
    public int ChampionshipId { get; set; }
    public Guid OrganizerId { get; set; }
}