using PlayOffsApi.Enum;

namespace PlayOffsApi.Models;

public class Match
{
    public int Id { get; set; }
    public int Winner { get; set; }
    public int Home { get; set; }
    public int Visitor { get; set; }
    public string Arbitrator { get; set; }
    public int ChampionshipId { get; set; }
    public DateTime Date { get; set; }
    public int Round { get; set; }
    public Phase Phase { get; set; }

    public Match(int championshipId, int home, int visitor, Phase phase)
    {
        Home = home;
        Visitor = visitor;
        Phase = phase;
        ChampionshipId = championshipId;
    }
    public Match(int championshipId, int home, int visitor, int round)
    {
        Home = home;
        Visitor = visitor;
        Round = round;
        ChampionshipId = championshipId;
    }
    public Match() { }
}