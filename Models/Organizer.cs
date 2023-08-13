using PlayOffsApi.Enum;

namespace PlayOffsApi.Models;

public class Organizer
{
    public Guid OrganizerId { get; set; }
    public int ChampionshipId { get; set; }
    public bool MainOrganizer { get; set; }
    public ChampionshipStatus ChampionshipStatus { get; set; }
}