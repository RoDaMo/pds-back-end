namespace PlayOffsApi.Models;

public class Goal
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int TeamId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid PlayerTempId { get; set; }
    public int Set { get; set; }
    public bool OwnGoal { get; set; }
    public Goal() { }
}