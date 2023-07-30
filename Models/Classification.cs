namespace PlayOffsApi.Models;

public class Classification
{
    public int Id { get; set; }
    public int Points { get; set; }
    public int TeamId { get; set; }
    public int ChampionshipId { get; set; }
    public int Position { get; set; }
    public Classification(int points, int teamId, int championshipId, int position)
    {
        Points = points;
        TeamId = teamId;
        ChampionshipId = championshipId;
        Position = position;
    }
    public Classification() {}
}