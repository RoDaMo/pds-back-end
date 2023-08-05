
using FluentValidation;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class MatchService
{
    private readonly DbService _dbService;
    private readonly BracketingService _bracketingService;

    public MatchService(DbService dbService, BracketingService bracketingService)
    {
        _dbService = dbService;
        _bracketingService = bracketingService;
    }

    public async Task EndGameToSimpleKnockouteValidationAsync(int matchId)
    {
        if(!await CheckIfMatchExists(matchId))
        {
            throw new ApplicationException("Partida passada não existe.");
        }
        if(await DepartureDateNotSet(matchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }
        if(await DidMatchNotStart(matchId))
        {
            throw new ApplicationException("Partida ainda não inciada ou já encerrada.");
        }
        if(await CheckIfThereIsWinner(matchId))
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }
        var match = await GetMatchById(matchId);
        var visitorPoints = await GetPointsFromTeamById(matchId, match.Visitor);
        var homePoints = await GetPointsFromTeamById(matchId, match.Home);
        if(homePoints > visitorPoints)
        {
            await DefineWinner(match.Home, matchId);
        }
        else if(homePoints < visitorPoints)
        {
            await DefineWinner(match.Visitor, matchId);
        } 
        else 
        {
            throw new ApplicationException("Partida não pode ser encerrada sem um vencedor.");
        }
    }
    private async Task<int> DefineWinner(int teamId, int matchId)
    {
        var id = await UpdateMatchToDefineWinner(teamId, matchId);
        var match = await GetMatchById(matchId);
        if(await CheckIfMatchesOfCurrentPhaseHaveEnded(match.ChampionshipId, match.Phase) && match.Phase != Phase.Finals)
        {
            var matches = await _dbService.GetAll<Match>("SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Phase = @phase ORDER BY Id", new {match.ChampionshipId, match.Phase});
            var newPhase = match.Phase + 1;
            for (int i = 0; i <= matches.Count() / 2; i = i + 2)
            {
                var newMatch = new Match(match.ChampionshipId, matches[i].Winner, matches[i+1].Winner, newPhase);
                await CreateMatchSend(newMatch);
            }

        }
        return id;
    }
    private async Task<Match> CreateMatchSend(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase) VALUES(@ChampionshipId, @Home, @Visitor, @Phase) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
    private async Task<bool> CheckIfMatchesOfCurrentPhaseHaveEnded(int championshipId, Phase phase)
    {
        var numberOfMatches = await _dbService.GetAsync<int>("SELECT Count(*) from matches WHERE ChampionshipId = @championshipId AND Phase = @phase", new {championshipId, phase});
        var numberOfMatchesClosed = await _dbService.GetAsync<int>("SELECT Count(*) from matches WHERE ChampionshipId = @championshipId AND Phase = @phase AND Winner IS NOT NULL", new {championshipId, phase});
        
        if(numberOfMatches != numberOfMatchesClosed)
        {
            return false;
        }
        return true;
    }
    private async Task<bool> CheckIfMatchExists(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId)", new {matchId});
    private async Task<Match> GetMatchById(int matchId)
        => await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @matchId", new{matchId});
    private async Task<int> GetPointsFromTeamById(int matchId, int teamId)
        => await _dbService.GetAsync<int>("SELECT COUNT(*) FROM goals WHERE MatchId = @matchId AND (TeamId = @teamId AND OwnGoal = false OR TeamId <> @teamId AND OwnGoal = true)", new {matchId, teamId});
    private async Task<bool> DidMatchNotStart(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date <> CURRENT_DATE);", new {matchId});
    private async Task<bool> DepartureDateNotSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date IS NULL);", new {matchId});
    private async Task<bool> CheckIfThereIsWinner(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND Winner IS NOT NULL)", new {matchId});

    public async Task EndGameToLeagueSystemValidationAsync(int matchId)
    {
        if(!await CheckIfMatchExists(matchId))
        {
            throw new ApplicationException("Partida passada não existe.");
        }
        if(await DepartureDateNotSet(matchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }
        if(await DidMatchNotStart(matchId))
        {
            throw new ApplicationException("Partida ainda não inciada ou já encerrada.");
        }
        if(await CheckIfThereIsWinner(matchId))
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }
        if(await CheckIfThereIsTie(matchId))
        {
            throw new ApplicationException("Partida já terminou em empate.");
        }

        var match = await GetMatchById(matchId);

        // if(await CheckIfLastRoundHasNotFinished(match.Round-1, match.ChampionshipId))
        // {
        //     throw new ApplicationException("Rodada anterior ainda não terminou.");
        // }

        var visitorPoints = await GetPointsFromTeamById(matchId, match.Visitor);
        var homePoints = await GetPointsFromTeamById(matchId, match.Home);
        if(homePoints > visitorPoints)
        {
            await DefineWinnerToLeagueSystem(match.Home, match);
        }
        else if(homePoints < visitorPoints)
        {
            await DefineWinnerToLeagueSystem(match.Visitor, match);
        } 
        else 
        {
            await DefineWinnerToLeagueSystem(0, match);
        }

    }
    private async Task<bool> CheckIfThereIsTie(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND Tied = true)", new {matchId});
    // private async Task<bool> CheckIfLastRoundHasNotFinished(int round, int championshipId)
    //     => await _dbService.GetAsync<bool>(
    //         "SELECT EXISTS(SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Round = @round AND Winner IS NULL AND Tied <> true)",
    //         new {championshipId, round});  
    //  private async Task<bool> CheckIfLastRoundHasNotFinishedInGroupStage(int round, int championshipId, List<int> teamsId)
    //   => await _dbService.GetAsync<bool>(
    //         @"SELECT EXISTS(
    //             SELECT * FROM matches 
    //             WHERE ChampionshipId = @championshipId AND 
    //             Round = @round AND 
    //             Winner IS NULL AND 
    //             Tied <> true AND
    //             Home = ANY(@teamsId)
    //         )",
    //         new {championshipId, round, teamsId});  

    private async Task<int> DefineWinnerToLeagueSystem(int winnerTeamId, Match match)
    {
        if(winnerTeamId == 0)
        {
            await UpdateMatchToDefineTie(match.Id);
            var homeClassificationId = await AssignPoints(match.Home, match.ChampionshipId, 1);
            var visitorClassificationId = await AssignPoints(match.Visitor, match.ChampionshipId, 1);
            var homeClassification = await GetClassificationById(homeClassificationId);
            var classifications = await PickUpTeamsToChangePositions(
                    homeClassification.Points, 
                    homeClassification.Position, 
                    homeClassification.ChampionshipId
                    );
            await ChangePosition(classifications, homeClassification);

            var visitorClassification = await GetClassificationById(visitorClassificationId);
            var classifications2 = await PickUpTeamsToChangePositions(
                    visitorClassification.Points, 
                    visitorClassification.Position, 
                    visitorClassification.ChampionshipId
                    );
            await ChangePosition(classifications2, visitorClassification);
            return winnerTeamId;
        }

        await UpdateMatchToDefineWinner(winnerTeamId, match.Id);
        var winnerClassificationId = await AssignPoints(winnerTeamId, match.ChampionshipId, 3);
        var winnerClassification = await GetClassificationById(winnerClassificationId);
        var classifications3 = await PickUpTeamsToChangePositions(
                winnerClassification.Points, 
                winnerClassification.Position, 
                winnerClassification.ChampionshipId
                );
        await ChangePosition(classifications3, winnerClassification);
        return winnerTeamId;
    }
    private async Task<int> UpdateMatchToDefineWinner(int teamId, int matchId)
    {
        if(teamId == 0)
        {
            return await _dbService.EditData("UPDATE matches SET Tied = true WHERE id = @matchId returning id", new {teamId, matchId});
        }
        return await _dbService.EditData("UPDATE matches SET Winner = @teamId WHERE id = @matchId returning id", new {teamId, matchId});
    }
    private async Task UpdateMatchToDefineTie(int matchId)
    {
        await _dbService.EditData("UPDATE matches SET Tied = true WHERE Id = @matchId", new {matchId});
    }
    private async Task<int> AssignPoints(int teamId, int championshipId, int points)
        => await _dbService.EditData(
            "UPDATE classifications SET Points = Points + @points WHERE ChampionshipId = @championshipId AND TeamId = @teamId returning Id",
            new {points, championshipId, teamId});
    private async Task<List<Classification>> PickUpTeamsToChangePositions(int points, int position, int championshipId)
        => await _dbService.GetAll<Classification>(
            "SELECT * FROM classifications WHERE ChampionshipId = @championshipId AND Position < @position AND Points <= @points ORDER BY Position", 
            new {championshipId, position, points});
    private async Task<List<Classification>> PickUpTeamsToChangePositions(int points, int position, int championshipId, List<int> group)
        => await _dbService.GetAll<Classification>(
            "SELECT * FROM classifications WHERE ChampionshipId = @championshipId AND Position < @position AND Points <= @points AND TeamId = ANY(@group) ORDER BY Position", 
            new {championshipId, position, points, group});
    private async Task<Classification> GetClassificationById(int classificationId)
        => await _dbService.GetAsync<Classification>(
            "SELECT * FROM classifications WHERE Id = @classificationId",
            new {classificationId});
    private async Task UpdatePositionClassification(int classificationId, int position)
    {
        await _dbService.EditData(
            "UPDATE classifications SET Position = @position WHERE Id = @classificationId",
            new {position, classificationId});
    }
    private async Task<int> AmountOfWins(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            "SELECT COUNT(*) FROM matches WHERE ChampionshipId = @championshipId AND Winner = @teamId", 
            new {teamId, championshipId});
    private async Task<int> GoalDifference(int teamId, int championshipId)
    {
        var goalsScored = await ProGoals(teamId, championshipId);
        var goalsConceded = await _dbService.GetAsync<int>(
            @"SELECT COUNT(g.Id)
            FROM Goals g
            JOIN Matches m ON g.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND
            (m.Visitor = @teamId OR m.Home = @teamId) AND 
            (g.TeamId <> @teamId AND g.OwnGoal = false OR g.TeamId = @teamId AND g.OwnGoal = true)
            GROUP BY g.TeamId;",
            new { championshipId, teamId });
        return goalsScored - goalsConceded;
    }
    private async Task<int> ProGoals(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(g.Id)
            FROM Goals g
            JOIN Matches m ON g.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND 
            (g.TeamId = @teamId AND g.OwnGoal = false OR g.TeamId <> @teamId AND g.OwnGoal = true)
            GROUP BY g.TeamId;",
            new { championshipId, teamId });
    private async Task<int> HeadToHeadWins(int teamId1, int teamId2, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(*) FROM matches 
            WHERE ChampionshipId = @championshipId AND
            Winner = @teamId1 AND
            (Visitor = @teamId2 OR Home = @teamId2)", 
            new {championshipId, teamId1, teamId2});
    private async Task<int> QualifyingGoal(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(g.Id)
            FROM Goals g
            JOIN Matches m ON g.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND 
            (g.TeamId = @teamId AND g.OwnGoal = false OR g.TeamId <> @teamId AND g.OwnGoal = true) AND
            m.Visitor = @TeamId
            GROUP BY g.TeamId;",
            new { championshipId, teamId });
    private async Task ChangePosition(List<Classification> classifications, Classification homeClassification)
    {
        for (int i = 0; i < classifications.Count(); i++)
        {
            if(classifications[i].Points < homeClassification.Points)
            {
                var aux = classifications[i].Position;
                classifications[i].Position = homeClassification.Position;
                homeClassification.Position = aux;
                for (int j = i; j < classifications.Count() - 1; j++)
                {
                    var aux2 = classifications[j].Position;
                    classifications[j].Position = classifications[j+1].Position;
                    classifications[j+1].Position = aux2;
                }

                for (int j = i; j < classifications.Count(); j++)
                {
                    await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                }
                await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                break;

            }
            else
            {
                var arrayTeamWins = await AmountOfWins(classifications[i].TeamId, classifications[i].ChampionshipId);
                var homeTeamWins = await AmountOfWins(homeClassification.TeamId, homeClassification.ChampionshipId);
                if(arrayTeamWins < homeTeamWins)
                {
                    var aux = classifications[i].Position;
                    classifications[i].Position = homeClassification.Position;
                    homeClassification.Position = aux;
                    for (int j = i; j < classifications.Count() - 1; j++)
                    {
                        var aux2 = classifications[j].Position;
                        classifications[j].Position = classifications[j+1].Position;
                        classifications[j+1].Position = aux2;
                    }

                    for (int j = i; j < classifications.Count(); j++)
                    {
                        await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                    }
                    await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                    break;
                }

                else if(arrayTeamWins == homeTeamWins)
                {                    
                    var arrayTeamGoalDifference = await GoalDifference(classifications[i].TeamId, classifications[i].ChampionshipId);
                    var homeTeamGoalDifference = await GoalDifference(homeClassification.TeamId, homeClassification.ChampionshipId);

                    if(arrayTeamGoalDifference < homeTeamGoalDifference)
                    {
                        var aux = classifications[i].Position;
                        classifications[i].Position = homeClassification.Position;
                        homeClassification.Position = aux;
                
                        for (int j = i; j < classifications.Count() - 1; j++)
                        {
                            var aux2 = classifications[j].Position;
                            classifications[j].Position = classifications[j+1].Position;
                            classifications[j+1].Position = aux2;
                        }

                        for (int j = i; j < classifications.Count(); j++)
                        {
                            await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                        }
                        await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                        break;
                    }

                    else if(arrayTeamGoalDifference == homeTeamGoalDifference)
                    { 
                        var arrayTeamProGoals = await ProGoals(classifications[i].TeamId, classifications[i].ChampionshipId);
                        var homeTeamProGoals = await ProGoals(homeClassification.TeamId, homeClassification.ChampionshipId);

                        if(arrayTeamProGoals < homeTeamProGoals)
                        {
                            var aux = classifications[i].Position;
                            classifications[i].Position = homeClassification.Position;
                            homeClassification.Position = aux;
                            for (int j = i; j < classifications.Count() - 1; j++)
                            {
                                var aux2 = classifications[j].Position;
                                classifications[j].Position = classifications[j+1].Position;
                                classifications[j+1].Position = aux2;
                            }

                            for (int j = i; j < classifications.Count(); j++)
                            {
                                await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                            }
                            await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                            break;
                        }

                        else if(arrayTeamProGoals == homeTeamProGoals)
                        {  
                            var arrayTeamDirectConfrontation = await HeadToHeadWins(
                                    classifications[i].TeamId,
                                    homeClassification.TeamId,
                                    classifications[i].ChampionshipId
                                );
                            var homeTeamDirectConfrontation = await HeadToHeadWins(
                                    homeClassification.TeamId,
                                    classifications[i].TeamId,
                                    homeClassification.ChampionshipId
                                );
                            if(arrayTeamDirectConfrontation < homeTeamDirectConfrontation)
                            {
                                var aux = classifications[i].Position;
                                classifications[i].Position = homeClassification.Position;
                                homeClassification.Position = aux;
                                for (int j = i; j < classifications.Count() - 1; j++)
                                {
                                    var aux2 = classifications[j].Position;
                                    classifications[j].Position = classifications[j+1].Position;
                                    classifications[j+1].Position = aux2;
                                }

                                for (int j = i; j < classifications.Count(); j++)
                                {
                                    await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                                }
                                await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                                break;
                            }
                            //cards would be here
                            else if(arrayTeamDirectConfrontation == homeTeamDirectConfrontation)
                            {                               
                                var arrayTeamQualifyingGoal = await QualifyingGoal(classifications[i].TeamId, classifications[i].ChampionshipId);
                                var homeTeamQualifyingGoal = await QualifyingGoal(homeClassification.TeamId, homeClassification.ChampionshipId);

                                if(arrayTeamQualifyingGoal < homeTeamQualifyingGoal)
                                {
                                    var aux = classifications[i].Position;
                                    classifications[i].Position = homeClassification.Position;
                                    homeClassification.Position = aux;
                                    for (int j = i; j < classifications.Count() - 1; j++)
                                    {
                                        var aux2 = classifications[j].Position;
                                        classifications[j].Position = classifications[j+1].Position;
                                        classifications[j+1].Position = aux2;
                                    }

                                    for (int j = i; j < classifications.Count(); j++)
                                    {
                                        await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                                    }
                                    await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                                    break;
                                }

                                else if(arrayTeamQualifyingGoal == homeTeamQualifyingGoal)
                                {
                                    Random random = new Random();
                                    int randomNumber = random.Next(0, 1);
                                    if(randomNumber == 1)
                                    {
                                        var aux = classifications[i].Position;
                                        classifications[i].Position = homeClassification.Position;
                                        homeClassification.Position = aux;
                                        for (int j = i; j < classifications.Count() - 1; j++)
                                        {
                                            var aux2 = classifications[j].Position;
                                            classifications[j].Position = classifications[j+1].Position;
                                            classifications[j+1].Position = aux2;
                                        }

                                        for (int j = i; j < classifications.Count(); j++)
                                        {
                                            await UpdatePositionClassification(classifications[j].Id, classifications[j].Position);
                                        }
                                        await UpdatePositionClassification(homeClassification.Id, homeClassification.Position);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public async Task EndGameToSimpleKnockoutGroupStageValidationAsync(int matchId)
    {
        if(!await CheckIfMatchExists(matchId))
        {
            throw new ApplicationException("Partida passada não existe.");
        }
        if(await DepartureDateNotSet(matchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }
        if(await DidMatchNotStart(matchId))
        {
            throw new ApplicationException("Partida ainda não inciada ou já encerrada.");
        }
        if(await CheckIfThereIsWinner(matchId))
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }
        if(await CheckIfThereIsTie(matchId))
        {
            throw new ApplicationException("Partida já terminou em empate.");
        }

        var match = await GetMatchById(matchId);
        // var teamsIdInGroup = await GetTeamsInGroup(match.Home, match.ChampionshipId);

        // if(await CheckIfLastRoundHasNotFinishedInGroupStage(match.Round-1, match.ChampionshipId, teamsIdInGroup))
        // {
        //     throw new ApplicationException("Rodada anterior ainda não terminou.");
        // }

        var visitorPoints = await GetPointsFromTeamById(matchId, match.Visitor);
        var homePoints = await GetPointsFromTeamById(matchId, match.Home);
        if(homePoints > visitorPoints)
        {
            await DefineWinnerToGroupStage(match.Home, match);
        }
        else if(homePoints < visitorPoints)
        {
            await DefineWinnerToGroupStage(match.Visitor, match);
        } 
        else 
        {
            await DefineWinnerToGroupStage(0, match);
        }

        if(!await CheckIfGroupStageEnded(match.ChampionshipId))
        {
            var classifications = await _dbService.GetAll<Classification>("SELECT * FROM classifications WHERE ChampionshipId = @championshipId ORDER BY Id", 
            new {match.ChampionshipId});
            var teamsId =  classifications.Where(c => c.Position == 1 || c.Position == 2).OrderBy(c => c.Position).Select(c => c.TeamId).ToList();   
            await _bracketingService.CreateSimpleknockoutToGroupStageValidationAsync(teamsId, match.ChampionshipId);
        }
    }

    private async Task<bool> CheckIfGroupStageEnded(int championshipId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Winner IS NULL AND Tied <> true)",
        new {championshipId});

    private async Task<int> DefineWinnerToGroupStage(int winnerTeamId, Match match)
    {
        if(winnerTeamId == 0)
        {
            await UpdateMatchToDefineTie(match.Id);
            var homeClassificationId = await AssignPoints(match.Home, match.ChampionshipId, 1);
            var visitorClassificationId = await AssignPoints(match.Visitor, match.ChampionshipId, 1);
            var homeClassification = await GetClassificationById(homeClassificationId);
            var homeGroup = await GetTeamsInGroup(match.Home, match.ChampionshipId);
            var classifications = await PickUpTeamsToChangePositions(
                    homeClassification.Points, 
                    homeClassification.Position, 
                    homeClassification.ChampionshipId,
                    homeGroup
                    );
            await ChangePosition(classifications, homeClassification);

            var visitorClassification = await GetClassificationById(visitorClassificationId);
            var visitorGroup = await GetTeamsInGroup(match.Home, match.ChampionshipId);
            var classifications2 = await PickUpTeamsToChangePositions(
                    visitorClassification.Points, 
                    visitorClassification.Position, 
                    visitorClassification.ChampionshipId,
                    visitorGroup
                    );
            await ChangePosition(classifications2, visitorClassification);
            return winnerTeamId;
        }

        await UpdateMatchToDefineWinner(winnerTeamId, match.Id);
        var winnerClassificationId = await AssignPoints(winnerTeamId, match.ChampionshipId, 3);
        var winnerClassification = await GetClassificationById(winnerClassificationId);
        var winnerGroup = await GetTeamsInGroup(winnerTeamId, match.ChampionshipId);
        var classifications3 = await PickUpTeamsToChangePositions(
                winnerClassification.Points, 
                winnerClassification.Position, 
                winnerClassification.ChampionshipId,
                winnerGroup
                );
        await ChangePosition(classifications3, winnerClassification);
        return winnerTeamId;
    }

    private async Task<List<int>> GetTeamsInGroup(int teamId, int championshipId)
    {
        var teamsId = await _dbService.GetAll<int>("SELECT TeamId FROM classifications WHERE ChampionshipId = @championshipId ORDER BY Id", new {championshipId});
        var teamsIdInGroup = new List<int>();
        var position = 0;
        for (int i = 0; i < teamsId.Count(); i++)
        {
            if(teamsId[i] == teamId)
                position = i;
        }
        for (int i = 0; i < teamsId.Count(); i++)
        {
            double calculation = i/4;
            double calculation2 = position/4;
            if(Math.Ceiling(calculation) == Math.Ceiling(calculation2))
            {
               teamsIdInGroup.Add(teamsId[i]);
            }
        }

        return teamsIdInGroup;
    }
}