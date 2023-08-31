
public class Foul
{
    public int Id { get; set; }
    public bool YellowCard { get; set; }
    public bool Considered { get; set; }
    public int MatchId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid PlayerTempId { get; set; }
    public int Minutes { get; set; }
    public bool Valid { get; set; }
    public Foul() {  }
}