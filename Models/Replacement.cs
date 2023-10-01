namespace PlayOffsApi.Models;

public class Replacement
{
    public Guid ReplacedId { get; set; }
    public Guid ReplacedTempId { get; set; }
    public Guid ReplacerId { get; set; }
    public Guid ReplacerTempId { get; set; }
    public int MatchId { get; set; }
    public int TeamId { get; set; }
}