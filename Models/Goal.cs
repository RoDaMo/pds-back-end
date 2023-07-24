namespace PlayOffsApi.Models;

public class Goal
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int TeamId { get; set; }
    public int NumberOfGoals { get; set; }
}