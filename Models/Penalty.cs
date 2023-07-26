namespace PlayOffsApi.Models;

public class Penalty
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int TeamId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid PlayerTempId { get; set; }
    public bool Converted { get; set; }
    public Penalty() { }
}