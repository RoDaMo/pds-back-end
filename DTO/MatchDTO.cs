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
    public bool IsSoccer { get; set; }
    public string WinnerName { get; set; }
    public int HomeId { get; set; }
    public int VisitorId { get; set; }
    public bool Finished { get; set; }
    public string Local { get; set; }
    public string Arbitrator { get; set; }
    public DateTime Date { get; set; }
    public int ChampionshipId { get; set; }
    public bool Prorrogation { get; set; }

    public MatchDTO()
    {
        Finished = false;
        IsSoccer = false;
    }
}