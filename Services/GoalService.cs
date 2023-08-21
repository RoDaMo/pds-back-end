using FluentValidation;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;

public class GoalService
{
    private readonly DbService _dbService;
    private readonly BracketingService _bracketingService;
    public GoalService(DbService dbService, BracketingService bracketingService)
    {
        _dbService = dbService;
        _bracketingService = bracketingService;
    }

    public async Task<List<string>> CreateGoalValidationAsync(Goal goal)
    {
        var errorMessages = new List<string>();
        var goalValidator = new GoalValidator();
        var championship = await GetChampionshipByMatchId(goal.MatchId);
        var match = await GetMatchById(goal.MatchId);
        
		var result = (championship.SportsId == Sports.Football) 
        ? goalValidator.Validate(goal, options => options.IncludeRuleSets("ValidationSoccer"))
        : goalValidator.Validate(goal, options => options.IncludeRuleSets("ValidationVolleyBall"));

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

        if(match.HomeUniform is null || match.VisitorUniform is null)
            throw new ApplicationException("É necessário definir os uniformes das equipes antes");
        
        if (match.Local is null)
            throw new ApplicationException("É necessário definir o local da partida antes antes");

        if(goal.PlayerId == Guid.Empty)
        {
            if(!await CheckRelationshipBetweenPlayerTempAndTeam(goal.PlayerTempId, goal.TeamId))
            {
                throw new ApplicationException("Jogador não pertence ao time informado.");
            }
        }
        else
        {
            if(!await CheckRelationshipBetweenPlayerAndTeam(goal.PlayerId, goal.TeamId))
            {
                throw new ApplicationException("Jogador não pertence ao time informado.");
            }
        }

        if(!await CheckRelationshipBetweenTeamAndMatch(goal.TeamId, goal.MatchId))
        {
            throw new ApplicationException("Time informado não participa da partida atual.");
        }
        if(await DepartureDateNotSet(goal.MatchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }

        if(await CheckIfThereIsWinner(goal.MatchId))
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }
        if(await CheckIfThereIsTie(goal.MatchId))
        {
            throw new ApplicationException("Partida já terminou em empate.");
        }
        if(goal.AssisterPlayerId != Guid.Empty)
        {
            if(!await CheckIfAssisterPlayerAndMarkerAreFromSameTeam(goal.AssisterPlayerId, goal.TeamId))
            {
                throw new ApplicationException("Jogador que fez assistência não pertence ao mesmo time do jogador que fez o gol.");
            }
        }
        else if(goal.AssisterPlayerTempId != Guid.Empty)
        {
            if(!await CheckIfAssisterPlayerTempAndMarkerAreFromSameTeam(goal.AssisterPlayerTempId, goal.TeamId))
            {
                throw new ApplicationException("Jogador que fez assistência não pertence ao mesmo time do jogador que fez o gol.");
            }
        }
        if(await CheckIfMinutesIsNotValid(goal, match))
            throw new ApplicationException("Tempo do evento é inválido");
        if(championship.SportsId == Sports.Football)
        {
            goal.Date = null;
            if(await CheckIfThereIsAnyPenaltyByMatchId(goal.MatchId))
            {
                throw new ApplicationException("Durante a etapa de pênaltis não é possível atribuir um gol normal.");
            }
            if(goal.PlayerId == Guid.Empty)
            {
                await CreateGoalToPlayerTempSend(goal);
            }
            else
            {
                await CreateGoalToPlayerSend(goal);
            }
        }
        else
        {
            goal.Minutes = null;
            goal.Date = goal.Date?.ToUniversalTime();
            if(await ThereIsAWinner(goal.MatchId))
            {
                throw new ApplicationException("Partida já encerrada.");
            }
            if(await SetIsInvalid(goal.MatchId, goal.TeamId, goal.Set))
            {
                throw new ApplicationException("Set inválido.");
            }
            if(goal.PlayerId == Guid.Empty)
            {
                await CreateGoalToPlayerTempSend(goal);
            }
            else
            {
                await CreateGoalToPlayerSend(goal);
            }

            await EndGame(goal.MatchId, goal.TeamId, goal.OwnGoal, championship.Format);
        }
        return errorMessages;
    }
    private async Task<bool> CheckIfMinutesIsNotValid(Goal goal, Match math)
    {
        if(goal.Set == 0)
        {
            var timeOfLastEvent = await GetTimeOfLastEventSoccer(goal.MatchId);
            if(math.Prorrogation)
            {
                if(goal.Minutes > 120 || timeOfLastEvent > goal.Minutes || goal.Minutes < 90)
                    return true;
                return false;
            }
            else if(goal.Minutes > 90 || timeOfLastEvent > goal.Minutes)
            {
                return true;
            }
            return false;
        }

        var timeOfLastEvent2 = await GetTimeOfLastEventVolley(goal.MatchId);
        if(timeOfLastEvent2.ToUniversalTime() > goal.Date?.ToUniversalTime())
            return true;
        return false;
    }
    private async Task<int> GetTimeOfLastEventSoccer(int matchId)
        => await _dbService.GetAsync<int>("SELECT Minutes FROM Goals WHERE MatchId = @matchId ORDER BY Id DESC LIMIT 1", new {matchId});
     private async Task<DateTime> GetTimeOfLastEventVolley(int matchId)
        => await _dbService.GetAsync<DateTime>("SELECT Date FROM Goals WHERE MatchId = @matchId ORDER BY Id DESC LIMIT 1", new {matchId});
    private async Task<bool> CheckIfAssisterPlayerTempAndMarkerAreFromSameTeam(Guid assisterId, int teamId)
        => await _dbService.GetAsync<bool>(
            @"SELECT EXISTS (
                    SELECT *
                    FROM PlayerTempProfiles
                    WHERE Id = @assisterId AND TeamsId = @teamId
                )", 
            new {assisterId, teamId});
    private async Task<bool> CheckIfAssisterPlayerAndMarkerAreFromSameTeam(Guid assisterId, int teamId)
        => await _dbService.GetAsync<bool>(
            @"SELECT EXISTS (
                    SELECT *
                    FROM Users
                    WHERE Id = @assisterId AND PlayerTeamId = @teamId
                )", 
            new {assisterId, teamId});
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
    private async Task<bool> CheckIfThereIsTie(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND Tied = true)", new {matchId});
    private async Task<bool> CheckIfThereIsWinner(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND Winner IS NOT NULL)", new {matchId});
    private async Task<Championship> GetChampionshipByMatchId(int matchId)
        => await _dbService.GetAsync<Championship>(
            @"SELECT c.*
            FROM Championships c
            JOIN Matches m ON c.Id = m.ChampionshipId
            WHERE m.Id = @matchId;
            ", 
            new {matchId});
    private async Task<bool> CheckRelationshipBetweenPlayerTempAndTeam(Guid playerTempId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM playertempprofiles WHERE id = @playerTempId AND TeamsId = @teamId);", new {playerTempId, teamId});
    private async Task<bool> CheckRelationshipBetweenPlayerAndTeam(Guid playerId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE id = @playerId AND PlayerTeamId = @teamId);", new {playerId, teamId});
    private async Task<bool> CheckRelationshipBetweenTeamAndMatch(int teamId, int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND (home = @teamId OR visitor = @teamId) );", new {matchId, teamId});
    private async Task<bool> DepartureDateNotSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date IS NULL);", new {matchId});
    private async Task<bool> CheckIfThereIsAnyPenaltyByMatchId(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM penalties WHERE MatchId = @matchId)", new {matchId});
    private async Task<int> CreateGoalToPlayerTempSend(Goal goal)
    {
        var id = 0;
        if(goal.AssisterPlayerId == Guid.Empty && goal.AssisterPlayerTempId != Guid.Empty)
        {
            id = await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal, AssisterPlayerTempId, AssisterPlayerId, Minutes, Date) VALUES (@MatchId, @TeamId, null, @PlayerTempId, @Set, @OwnGoal, @AssisterPlayerTempId, null, @Minutes, @Date) RETURNING Id;",
			goal);
        }
        else if(goal.AssisterPlayerId != Guid.Empty)
        {
            id = await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal, AssisterPlayerId, AssisterPlayerTempId, Minutes, Date) VALUES (@MatchId, @TeamId, null, @PlayerTempId, @Set, @OwnGoal, @AssisterPlayerId, null, @Minutes, @Date) RETURNING Id;",
			goal);
        }
        else
        {
            id = await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal, AssisterPlayerId, AssisterPlayerTempId, Minutes, Date) VALUES (@MatchId, @TeamId, null, @PlayerTempId, @Set, @OwnGoal, null, null, @Minutes, @Date) RETURNING Id;",
			goal);
        }
        return id;
    }
        
    private async Task<int> CreateGoalToPlayerSend(Goal goal)
    {
        var id = 0;
        if(goal.AssisterPlayerId == Guid.Empty && goal.AssisterPlayerTempId != Guid.Empty)
        {
            id = await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal, AssisterPlayerTempId, AssisterPlayerId, Minutes, Date) VALUES (@MatchId, @TeamId, @PlayerId, null, @Set, @OwnGoal, @AssisterPlayerTempId, null, @Minutes, @Date) RETURNING Id;",
			goal);
        }
        else if(goal.AssisterPlayerId != Guid.Empty)
        {
            id = await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal, AssisterPlayerId, AssisterPlayerTempId, Minutes, Date) VALUES (@MatchId, @TeamId, @PlayerId, null, @Set, @OwnGoal, @AssisterPlayerId, null, @Minutes, @Date) RETURNING Id;",
			goal);
        }
        else
        {
            id = await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal, AssisterPlayerId, AssisterPlayerTempId, Minutes, Date) VALUES (@MatchId, @TeamId, @PlayerId, null, @Set, @OwnGoal, null, null, @Minutes, @Date) RETURNING Id;",
			goal);
        }
        return id;
    }
        
    private async Task<bool> ThereIsAWinner(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND winner IS NOT NULL);", new {matchId});
    private async Task<bool> SetIsInvalid(int matchId, int teamId, int set)
    {
        var lastSet = !await IsItFirstSet(matchId) ? 1 : await GetLastSet(matchId);
        var team1Points = await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId OR TeamId <> @teamId AND OwnGoal = true) AND Set = @lastSet", new {matchId, teamId, lastSet});
        var team2Points = await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId OR TeamId = @teamId AND OwnGoal = true) AND Set = @lastSet", new {matchId, teamId, lastSet});
        var finished = false;
        if(lastSet != 5)
        {
            if(team1Points == 25 && team2Points < 24)
            {
                finished = true;
            }
            else if(team1Points < 24 && team2Points == 25)
            {
                finished = true;
            }
            else if(team1Points >= 24 && team2Points >= 24)
            {
                if(team1Points - team2Points == 2)
                {
                    finished = true;
                }
                else if(team1Points - team2Points == -2)
                {
                    finished = true;
                }
            }
        }
        else
        {
            if(team1Points == 15 && team2Points < 14)
            {
                finished = true;
            }
            else if(team1Points < 14 && team2Points == 15)
            {
                finished = true;
            }
            else if(team1Points >= 14 && team2Points >= 14)
            {
                if(team1Points - team2Points == 2)
                {
                    finished = true;
                }
                else if(team1Points - team2Points == -2)
                {
                    finished = true;
                }
            }
        }

        if(finished)
        {
            if(lastSet + 1 == set)
            {
                return false;
            }
            return true;
        }
        else
        {
            if(lastSet == set)
            {
                return false;
            }
            return true;
        }
    }

    private async Task EndGame(int matchId, int teamId, bool ownGoal, Format format)
    {
        var match = await GetMatchById(matchId);
        var pointsForSet = new List<int>();
        var pointsForSet2 = new List<int>();
        var WonSets = 0;
        var WonSets2 = 0;
        var lastSet = !await IsItFirstSet(matchId) ? 1 : await GetLastSet(matchId);
        var team2Id = await _dbService.GetAsync<int>("SELECT CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches WHERE id = @matchId;", new {teamId, matchId});

        for (int i = 0;  i < lastSet; i++)
        {
            pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId And OwnGoal = false OR TeamId <> @teamId And OwnGoal = true) AND Set = @j", new {matchId, teamId, j = i+1}));
            pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId And OwnGoal = false OR TeamId = @teamId And OwnGoal = true) AND Set = @j", new {matchId, teamId, j = i+1}));
        }

        for (int i = 0;  i < lastSet; i++)
        {
            if(i != 4)
            {
                if(pointsForSet[i] == 25 && pointsForSet2[i] < 24)
                {
                    WonSets++;
                }
                else if(pointsForSet[i] < 24 && pointsForSet2[i] == 25)
                {
                    WonSets2++;
                }
                else if(pointsForSet[i] >= 24 && pointsForSet2[i] >= 24)
                {
                    if(pointsForSet[i] - pointsForSet2[i] == 2)
                    {
                        WonSets++;
                    }
                    else if(pointsForSet[i] - pointsForSet2[i] == -2)
                    {
                        WonSets2++;

                    }
                }
            }

            else
            {
                if(pointsForSet[i] == 15 && pointsForSet2[i] < 14)
                {
                    WonSets++;
                }
                else if(pointsForSet[i] < 14 && pointsForSet2[i] == 15)
                {
                    WonSets2++;
                }
                else if(pointsForSet[i] >= 14 && pointsForSet2[i] >= 14)
                {
                    if(pointsForSet[i] - pointsForSet2[i] == 2)
                    {
                        WonSets++;
                    }
                    else if(pointsForSet[i] - pointsForSet2[i] == -2)
                    {
                        WonSets2++;
                    }
                }
            }
        }

        if(format == Format.Knockout || match.Round == 0)
        {
            if(WonSets == 3 && !ownGoal)
            {
                await DefineWinner(teamId, matchId);
            }
            else if(WonSets2 == 3  && !ownGoal)
            {
                await DefineWinner(team2Id, matchId);
            }
            else if(WonSets2 == 3  && ownGoal)
            {
                await DefineWinner(team2Id, matchId);
            }
        }
        else if(format == Format.LeagueSystem)
        {
            if(WonSets == 3 && !ownGoal)
            {
                await DefineWinnerToLeagueSystem(teamId, match);
            }
            else if(WonSets2 == 3  && !ownGoal)
            {
                await DefineWinnerToLeagueSystem(team2Id, match);
            }
            else if(WonSets2 == 3  && ownGoal)
            {
                await DefineWinnerToLeagueSystem(team2Id, match);
            }
        }
        else if(format == Format.GroupStage)
        {
            if(WonSets == 3 && !ownGoal)
            {
                await DefineWinnerToGroupStage(teamId, match);
            }
            else if(WonSets2 == 3  && !ownGoal)
            {
                await DefineWinnerToGroupStage(team2Id, match);
            }
            else if(WonSets2 == 3  && ownGoal)
            {
                await DefineWinnerToGroupStage(team2Id, match);
            }

            if(!await CheckIfGroupStageEnded(match.ChampionshipId))
            {
                var classifications = await _dbService.GetAll<Classification>("SELECT * FROM classifications WHERE ChampionshipId = @championshipId ORDER BY Id", 
                new {match.ChampionshipId});
                var teamsId =  classifications.Where(c => c.Position == 1 || c.Position == 2).OrderBy(c => c.Position).Select(c => c.TeamId).ToList();   
                await _bracketingService.CreateKnockoutToGroupStageValidationAsync(teamsId, match.ChampionshipId);
            }
        }
    }
    private async Task<bool> CheckIfGroupStageEnded(int championshipId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Winner IS NULL AND Tied <> true)",
        new {championshipId});
    private async Task<List<Classification>> PickUpTeamsToChangePositions(int points, int position, int championshipId, List<int> group)
        => await _dbService.GetAll<Classification>(
            "SELECT * FROM classifications WHERE ChampionshipId = @championshipId AND Position < @position AND Points <= @points AND TeamId = ANY(@group) ORDER BY Position", 
            new {championshipId, position, points, group});
    private async Task<int> DefineWinnerToGroupStage(int winnerTeamId, Match match)
    {
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
    private async Task<bool> IsItFirstSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM goals WHERE MatchId = @matchId);", new {matchId});
    private async Task<int> GetLastSet(int matchId)
        => await _dbService.GetAsync<int>("SELECT MAX(Set) from goals where MatchId = @matchId", new {matchId});
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
    private async Task<int> UpdateMatchToDefineWinner(int teamId, int matchId)
        => await _dbService.EditData("UPDATE matches SET Winner = @teamId WHERE id = @matchId returning id", new {teamId, matchId});
    
    private async Task<Match> GetMatchById(int matchId)
        => await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @matchId", new{matchId});
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
    private async Task<Match> CreateMatchSend(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase) VALUES(@ChampionshipId, @Home, @Visitor, @Phase) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}

    private async Task<int> DefineWinnerToLeagueSystem(int winnerTeamId, Match match)
    {
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
    
    private async Task<int> AssignPoints(int teamId, int championshipId, int points)
        => await _dbService.EditData(
            "UPDATE classifications SET Points = Points + @points WHERE ChampionshipId = @championshipId AND TeamId = @teamId returning Id",
            new {points, championshipId, teamId});
    private async Task<List<Classification>> PickUpTeamsToChangePositions(int points, int position, int championshipId)
        => await _dbService.GetAll<Classification>(
            "SELECT * FROM classifications WHERE ChampionshipId = @championshipId AND Position < @position AND Points <= @points ORDER BY Position", 
            new {championshipId, position, points});
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
    private async Task<int> ProGoals(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(g.Id)
            FROM Goals g
            JOIN Matches m ON g.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND 
            (g.TeamId = @teamId AND g.OwnGoal = false OR g.TeamId <> @teamId AND g.OwnGoal = true)
            GROUP BY g.TeamId;",
            new { championshipId, teamId });
    private async Task<int> WinningSets(int teamId, int championshipId)
    {
        var matches = await _dbService.GetAll<Match>(
            "SELECT * FROM Matches WHERE (Visitor = @teamId OR Home = @teamId) AND ChampionshipId = @championshipId",
            new {teamId, championshipId});
        var allSetsWon = 0;
         
        foreach (var match in matches)
        {
            var pointsForSet = new List<int>();
            var pointsForSet2 = new List<int>();
            var WonSets = 0;
            var WonSets2 = 0;
            var lastSet = 0;
            lastSet = !await IsItFirstSet(match.Id) ? 1 : await GetLastSet(match.Id);
            var team2Id = await _dbService.GetAsync<int>("SELECT CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches WHERE id = @matchId;", new {teamId, matchId = match.Id});

            for (int i = 0;  i < lastSet; i++)
            {
                pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId And OwnGoal = false OR TeamId <> @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId, j = i+1}));
                pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId And OwnGoal = false OR TeamId = @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId, j = i+1}));
            }

            for (int i = 0;  i < lastSet; i++)
            {
                if(i != 4)
                {
                    if(pointsForSet[i] == 25 && pointsForSet2[i] < 24)
                    {
                        WonSets++;
                    }
                    else if(pointsForSet[i] < 24 && pointsForSet2[i] == 25)
                    {
                        WonSets2++;
                    }
                    else if(pointsForSet[i] >= 24 && pointsForSet2[i] >= 24)
                    {
                        if(pointsForSet[i] - pointsForSet2[i] == 2)
                        {
                            WonSets++;
                        }
                        else if(pointsForSet[i] - pointsForSet2[i] == -2)
                        {
                            WonSets2++;

                        }
                    }
                }

                else
                {
                    if(pointsForSet[i] == 15 && pointsForSet2[i] < 14)
                    {
                        WonSets++;
                    }
                    else if(pointsForSet[i] < 14 && pointsForSet2[i] == 15)
                    {
                        WonSets2++;
                    }
                    else if(pointsForSet[i] >= 14 && pointsForSet2[i] >= 14)
                    {
                        if(pointsForSet[i] - pointsForSet2[i] == 2)
                        {
                            WonSets++;
                        }
                        else if(pointsForSet[i] - pointsForSet2[i] == -2)
                        {
                            WonSets2++;

                        }
                    }

                }
            }

            allSetsWon = allSetsWon + WonSets;  
        }
        return allSetsWon;
    }

    private async Task<int> LosingSets(int teamId, int championshipId)
    {
        var matches = await _dbService.GetAll<Match>(
            "SELECT * FROM Matches WHERE (Visitor = @teamId OR Home = @teamId) AND ChampionshipId = @championshipId",
            new {teamId, championshipId});
        var allLosingSets = 0;
         
        foreach (var match in matches)
        {
            var pointsForSet = new List<int>();
            var pointsForSet2 = new List<int>();
            var WonSets = 0;
            var WonSets2 = 0;
            var lastSet = 0;
            lastSet = !await IsItFirstSet(match.Id) ? 1 : await GetLastSet(match.Id);
            var team2Id = await _dbService.GetAsync<int>("SELECT CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches WHERE id = @matchId;", new {teamId, matchId = match.Id});

            for (int i = 0;  i < lastSet; i++)
            {
                pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId And OwnGoal = false OR TeamId <> @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId, j = i+1}));
                pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId And OwnGoal = false OR TeamId = @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId, j = i+1}));
            }

            for (int i = 0;  i < lastSet; i++)
            {
                if(i != 4)
                {
                    if(pointsForSet[i] == 25 && pointsForSet2[i] < 24)
                    {
                        WonSets++;
                    }
                    else if(pointsForSet[i] < 24 && pointsForSet2[i] == 25)
                    {
                        WonSets2++;
                    }
                    else if(pointsForSet[i] >= 24 && pointsForSet2[i] >= 24)
                    {
                        if(pointsForSet[i] - pointsForSet2[i] == 2)
                        {
                            WonSets++;
                        }
                        else if(pointsForSet[i] - pointsForSet2[i] == -2)
                        {
                            WonSets2++;

                        }
                    }
                }

                else
                {
                    if(pointsForSet[i] == 15 && pointsForSet2[i] < 14)
                    {
                        WonSets++;
                    }
                    else if(pointsForSet[i] < 14 && pointsForSet2[i] == 15)
                    {
                        WonSets2++;
                    }
                    else if(pointsForSet[i] >= 14 && pointsForSet2[i] >= 14)
                    {
                        if(pointsForSet[i] - pointsForSet2[i] == 2)
                        {
                            WonSets++;
                        }
                        else if(pointsForSet[i] - pointsForSet2[i] == -2)
                        {
                            WonSets2++;

                        }
                    }
                }
            }
            allLosingSets = allLosingSets + WonSets2;  
        }
        return allLosingSets;
    }
    private async Task<int> PointsAgainst(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(g.Id)
            FROM Goals g
            JOIN Matches m ON g.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND
            (m.Visitor = @teamId OR m.Home = @teamId) AND 
            (g.TeamId <> @teamId AND g.OwnGoal = false OR g.TeamId = @teamId AND g.OwnGoal = true)
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
                    var arrayTeamWinningSets = await WinningSets(classifications[i].TeamId, classifications[i].ChampionshipId);
                    var homeTeamWinningSets = await WinningSets(homeClassification.TeamId, homeClassification.ChampionshipId);

                    if(arrayTeamWinningSets < homeTeamWinningSets)
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

                    else if(arrayTeamWinningSets == homeTeamWinningSets)
                    {
                        var arrayTeamLosingSets = await LosingSets(classifications[i].TeamId, classifications[i].ChampionshipId);
                        var homeTeamLosingSets = await LosingSets(homeClassification.TeamId, homeClassification.ChampionshipId);

                        if(arrayTeamLosingSets > homeTeamLosingSets)
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

                        else if(arrayTeamLosingSets == homeTeamLosingSets)
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
                                var arrayTeamPointsAgainst = await PointsAgainst(classifications[i].TeamId, classifications[i].ChampionshipId);
                                var homeTeamPointsAgainst = await PointsAgainst(homeClassification.TeamId, homeClassification.ChampionshipId);

                                if(arrayTeamPointsAgainst > homeTeamPointsAgainst)
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

                                else if(arrayTeamPointsAgainst == homeTeamPointsAgainst)
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
}