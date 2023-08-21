namespace PlayOffsApi.DTO;

public class ClassificationDTO
{
    public int Position { get; set; }
    public string Emblem { get; set; }
    public string Name { get; set; }
    public int Points { get; set; }
    public int AmountOfMatches { get; set; }
    public int Wins { get; set; }
    public int GoalBalance { get; set; }
    public int ProGoals { get; set; }
    public int YellowCard { get; set; }
    public int RedCard { get; set; }
    public int WinningSets { get; set; }
    public int LosingSets { get; set; }
    public int ProPoints { get; set; }
    public int PointsAgainst { get; set; }
    public List<MatchDTO> LastMatches { get; set; }
    public List<LastResultsDTO> LastResults { get; set; }

    // public ClassificationDTO(
    //     int position, string emblem, string name, int points, int amountOfMatches, int wins,
    //     int goalBalance, int proGoals, int yellowCard, int redCard, int winningSets,
    //     int losingSets, int proPoints, int pointsAgainst,
    //     List<MatchDTO> lastMatches, List<LastResultsDTO> lastResults
    // )
    //     {
    //         Position = position;
    //         Emblem = emblem;
    //         Name = name;
    //         Points = points;
    //         AmountOfMatches = amountOfMatches;
    //         Wins = wins;
    //         GoalBalance = goalBalance;
    //         ProGoals = proGoals;
    //         YellowCard = yellowCard;
    //         RedCard = redCard;
    //         WinningSets = winningSets;
    //         LosingSets = losingSets;
    //         ProPoints = proPoints;
    //         PointsAgainst = pointsAgainst;
    //         LastMatches = lastMatches;
    //         LastResults = lastResults;
    //     }

    public ClassificationDTO() {  }
}