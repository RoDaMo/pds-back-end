using PlayOffsApi.Models;

namespace PlayOffsApi.DTO;

public class Todo
{
    public bool Rules { get; set; }
    public bool CreatedBracketing { get; set; }
    public bool AddedEnoughTeams { get; set; }
    public bool AddedSuborganizers { get; set; }
    public List<MatchDTO> PendentMatches { get; set; }
}