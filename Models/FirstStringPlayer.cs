namespace PlayOffsApi.Models;

public class FirstStringPlayer
{
    public Guid? PlayerId { get; set; }
    public Guid? PlayerTempId { get; set; }
    public int MatchId { get; set; }
    public int TeamId { get; set; }
    public int Position { get; set; }
    public int Line { get; set; }
}