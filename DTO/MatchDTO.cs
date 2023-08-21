namespace PlayOffsApi.DTO;

public class MatchDTO
{
    public int Id { get; set; }
    public string HomeName { get; set; }
    public string VisitorName { get; set; }
    public string HomeEmblem { get; set; }
    public string VisitorEmblem { get; set; }
    public int HomeGoals { get; set; }
    public int VisitorGoals { get; set; }
    public int HomeWinnigSets { get; set; }
    public int VisitorWinnigSets { get; set; }
    public bool HasAggregatedScore { get; set; }
    public int HomeAggregatedScore { get; set; }
    public int VisitorAggregatedScore { get; set; }
}