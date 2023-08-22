using PlayOffsApi.DTO;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class StatisticsService
{
    private readonly DbService _dbService;

    public StatisticsService(DbService dbService)
    {
        _dbService = dbService;
    }
    public async Task<List<ClassificationDTO>> GetClassificationsValidationAsync(int championshipId)
    {
        var championship = await GetChampionshipByIdSend(championshipId);

        if(championship is null)
            throw new ApplicationException("Campeonato não existe");
        
        if(championship.Format == Enum.Format.Knockout)
            throw new ApplicationException("Estatísticas apenas para pontos corridos ou fase de grupos");
        
        if(championship.SportsId == Sports.Football)
        {
            var classifications = await GetAllClassificationsByChampionshipId(championshipId);
            var classificationsDTO = new List<ClassificationDTO>();

            if(championship.Format == Enum.Format.LeagueSystem)
            {
                classifications = classifications.OrderBy(c => c.Position).ToList();
                foreach (var classification in classifications)
                {
                    var classificationDTO = new ClassificationDTO();
                    var team = await GetByTeamIdSendAsync(classification.TeamId);
                    classificationDTO.Position = classification.Position;
                    classificationDTO.Points = classification.Points;
                    classificationDTO.Emblem = team.Emblem;
                    classificationDTO.Name = team.Name;
                    classificationDTO.TeamId = team.Id;
                    classificationDTO.Wins = await AmountOfWins(team.Id, championshipId);
                    classificationDTO.GoalBalance = await GoalDifference(team.Id, championshipId);
                    classificationDTO.ProGoals = await ProGoals(team.Id, championshipId);
                    classificationDTO.AmountOfMatches = await AmountOfMatches(team.Id, championshipId);
                    classificationDTO.LastMatches = await GetLast3Matches(team.Id, championshipId);
                    classificationDTO.LastResults = GetResults(classificationDTO);
                    classificationsDTO.Add(classificationDTO);
                }
                return classificationsDTO;
            }

            else if(championship.Format == Enum.Format.GroupStage)
            {
                var classificationsDTOOrdered = new List<Classification>();
                for (int i = 0; i < classifications.Count(); i += 4)
                {
                    List<Classification> group = classifications.Skip(i).Take(4).ToList();
                    group = group.OrderBy(c => c.Position).ToList();
                    classificationsDTOOrdered.AddRange(group);   
                }
                foreach (var classification in classificationsDTOOrdered)
                {
                    var classificationDTO = new ClassificationDTO();
                    var team = await GetByTeamIdSendAsync(classification.TeamId);
                    classificationDTO.Position = classification.Position;
                    classificationDTO.Points = classification.Points;
                    classificationDTO.TeamId = team.Id;
                    classificationDTO.Emblem = team.Emblem;
                    classificationDTO.Name = team.Name;
                    classificationDTO.Wins = await AmountOfWins(team.Id, championshipId);
                    classificationDTO.GoalBalance = await GoalDifference(team.Id, championshipId);
                    classificationDTO.ProGoals = await ProGoals(team.Id, championshipId);
                    classificationDTO.AmountOfMatches = await AmountOfMatches(team.Id, championshipId);
                    classificationDTO.LastMatches = await GetLast3Matches(team.Id, championshipId);
                    classificationDTO.LastResults = GetResults(classificationDTO);
                    classificationsDTO.Add(classificationDTO);
                }
                return classificationsDTO;
            }
        }

        else
        {
            var classifications = await GetAllClassificationsByChampionshipId(championshipId);
            var classificationsDTO = new List<ClassificationDTO>();

            if(championship.Format == Enum.Format.LeagueSystem)
            {
                classifications = classifications.OrderBy(c => c.Position).ToList();
                foreach (var classification in classifications)
                {
                    var classificationDTO = new ClassificationDTO();
                    var team = await GetByTeamIdSendAsync(classification.TeamId);
                    classificationDTO.Position = classification.Position;
                    classificationDTO.Points = classification.Points;
                    classificationDTO.Emblem = team.Emblem;
                    classificationDTO.Name = team.Name;
                    classificationDTO.TeamId = team.Id;
                    classificationDTO.Wins = await AmountOfWins(team.Id, championshipId);
                    classificationDTO.WinningSets = await WinningSets(team.Id, championshipId);
                    classificationDTO.LosingSets = await LosingSets(team.Id, championshipId);
                    classificationDTO.ProPoints = await ProGoals(team.Id, championshipId);
                    classificationDTO.PointsAgainst = await PointsAgainst(team.Id, championshipId);
                    classificationDTO.AmountOfMatches = await AmountOfMatches(team.Id, championshipId);
                    classificationDTO.LastMatches = await GetLast3MatchesVolley(team.Id, championshipId);
                    classificationDTO.LastResults = await GetResultsToVolley(classificationDTO, team.Id);
                    classificationsDTO.Add(classificationDTO);
                }
                return classificationsDTO;
            }

            else if(championship.Format == Enum.Format.GroupStage)
            {
                var classificationsDTOOrdered = new List<Classification>();
                for (int i = 0; i < classifications.Count(); i += 4)
                {
                    List<Classification> group = classifications.Skip(i).Take(4).ToList();
                    group = group.OrderBy(c => c.Position).ToList();
                    classificationsDTOOrdered.AddRange(group);   
                }
                foreach (var classification in classifications)
                {
                    var classificationDTO = new ClassificationDTO();
                    var team = await GetByTeamIdSendAsync(classification.TeamId);
                    classificationDTO.Position = classification.Position;
                    classificationDTO.Points = classification.Points;
                    classificationDTO.Emblem = team.Emblem;
                    classificationDTO.Name = team.Name;
                    classificationDTO.TeamId = team.Id;
                    classificationDTO.Wins = await AmountOfWins(team.Id, championshipId);
                    classificationDTO.WinningSets = await WinningSets(team.Id, championshipId);
                    classificationDTO.LosingSets = await LosingSets(team.Id, championshipId);
                    classificationDTO.ProPoints = await ProGoals(team.Id, championshipId);
                    classificationDTO.PointsAgainst = await PointsAgainst(team.Id, championshipId);
                    classificationDTO.AmountOfMatches = await AmountOfMatches(team.Id, championshipId);
                    classificationDTO.LastMatches = await GetLast3Matches(team.Id, championshipId);
                    classificationDTO.LastResults = await GetResultsToVolley(classificationDTO, team.Id);
                    classificationsDTO.Add(classificationDTO);
                }
                return classificationsDTO;
            }
        }
        return new();
    }
    private async Task<Championship> GetChampionshipByIdSend(int id) 
	    => await _dbService.GetAsync<Championship>("SELECT * FROM championships WHERE id = @id", new { id });
	private async Task<List<Classification>> GetAllClassificationsByChampionshipId(int championshipId)
        => await _dbService.GetAll<Classification>("SELECT * FROM classifications WHERE ChampionshipId = @ChampionshipId ORDER BY Id", new {championshipId});
	private async Task<Team> GetByTeamIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id AND deleted = false", new {id});
    private async Task<int> AmountOfWins(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            "SELECT COUNT(*) FROM matches WHERE ChampionshipId = @championshipId AND Winner = @teamId", 
            new {teamId, championshipId});
    private async Task<int> GoalDifference(int teamId, int championshipId)
    {
        var goalsScored = await ProGoals(teamId, championshipId);
        var goalsConceded = await _dbService.GetAsync<int>(
            @"SELECT  COALESCE(SUM(TotalGoals), 0) AS GrandTotalGoals
            FROM (
                SELECT g.TeamId, COUNT(g.Id) AS TotalGoals
                FROM Goals g
                JOIN Matches m ON g.MatchId = m.Id
                WHERE m.ChampionshipId = @championshipId AND
                    (m.Visitor = @teamId OR m.Home = @teamId) AND 
                    (g.TeamId <> @teamId AND g.OwnGoal = false OR g.TeamId = @teamId AND g.OwnGoal = true)
                GROUP BY g.TeamId
            ) AS SubqueryAlias;",
            new { championshipId, teamId });
        var result = goalsScored - goalsConceded;
        return result;
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
    private async Task<int> AmountOfMatches(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(Id) FROM matches 
            WHERE (Visitor = @teamId OR Home = @teamId) AND 
            ChampionshipId = @championshipId AND
            (Winner IS NOT NULL OR Tied = TRUE)", 
            new {teamId, championshipId});
    private async Task<List<MatchDTO>> GetLast3Matches(int teamId, int championshipId)
    {
        var matches = await _dbService.GetAll<Match>(
            @"SELECT * FROM matches 
            WHERE (Visitor = @teamId OR Home = @teamId) AND 
            ChampionshipId = @championshipId AND
            (Winner IS NOT NULL OR Tied = TRUE)
            ORDER BY Date DESC
            LIMIT 3", 
            new {teamId, championshipId});
        var matchesDTO = new List<MatchDTO>();
        foreach (var match in matches)
        {
            var matchDTO = new MatchDTO();
            var homeTeam = await GetByTeamIdSendAsync(match.Home);
            var visitorTeam = await GetByTeamIdSendAsync(match.Visitor);
            matchDTO.Id = match.Id;
            matchDTO.HomeEmblem = homeTeam.Emblem;
            matchDTO.HomeName = homeTeam.Name;
            matchDTO.HomeId = match.Home;
            matchDTO.IsSoccer = true;
            matchDTO.HomeGoals = await GetPointsFromTeamById(match.Id, match.Home);
            matchDTO.VisitorGoals = await GetPointsFromTeamById(match.Id, match.Visitor);
            matchDTO.VisitorEmblem = visitorTeam.Emblem;
            matchDTO.VisitorName = visitorTeam.Name;
            matchDTO.VisitorId = match.Visitor;
            matchDTO.Finished = true;
            matchesDTO.Add(matchDTO);
        }
        return matchesDTO;
    }
    private async Task<List<MatchDTO>> GetLast3MatchesVolley(int teamId, int championshipId)
    {
        var matches = await _dbService.GetAll<Match>(
            @"SELECT * FROM matches 
            WHERE (Visitor = @teamId OR Home = @teamId) AND 
            ChampionshipId = @championshipId AND
            (Winner IS NOT NULL OR Tied = TRUE)
            ORDER BY Date DESC
            LIMIT 3", 
            new {teamId, championshipId});
        var matchesDTO = new List<MatchDTO>();
        foreach (var match in matches)
        {
            var matchDTO = new MatchDTO();
            var homeTeam = await GetByTeamIdSendAsync(match.Home);
            var visitorTeam = await GetByTeamIdSendAsync(match.Visitor);
            matchDTO.Id = match.Id;
            matchDTO.HomeId = match.Home;
            matchDTO.HomeEmblem = homeTeam.Emblem;
            matchDTO.HomeName = homeTeam.Name;
            matchDTO.VisitorEmblem = visitorTeam.Emblem;
            matchDTO.VisitorName = visitorTeam.Name;
            matchDTO.VisitorId = match.Visitor;
            matchDTO.Finished = true;
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
            if(match.Home == teamId)
            {
                matchDTO.HomeWinnigSets = WonSets;
                matchDTO.VisitorWinnigSets = WonSets2;
            }
            else
            {
                matchDTO.HomeWinnigSets = WonSets2;
                matchDTO.VisitorWinnigSets = WonSets;
            }
            matchesDTO.Add(matchDTO);
        }
        return matchesDTO;
    }
    private async Task<int> GetPointsFromTeamById(int matchId, int teamId)
        => await _dbService.GetAsync<int>("SELECT COUNT(*) FROM goals WHERE MatchId = @matchId AND (TeamId = @teamId AND OwnGoal = false OR TeamId <> @teamId AND OwnGoal = true)", new {matchId, teamId});
   private List<LastResultsDTO> GetResults(ClassificationDTO classification)
   {
    var results = new List<LastResultsDTO>();
    foreach (var match in classification.LastMatches)
    {
        var result = new LastResultsDTO();
        if(match.HomeGoals == match.VisitorGoals)
        {
            result.Tied = true;
        }
        else if(match.HomeGoals > match.VisitorGoals)
        {
            if(match.HomeId == classification.TeamId)
            {
                result.Won = true;
            }
            else
            {
                result.Lose = true;
            }
        }
        else
        {
            if(match.HomeId == classification.TeamId)
            {
                result.Lose = true;
            }
            else
            {
                result.Won = true;
            }
        }

        results.Add(result);
    }
    return results;
   }
   private async Task<List<LastResultsDTO>> GetResultsToVolley(ClassificationDTO classificationDTO, int teamId)
   {
    var results = new List<LastResultsDTO>();
    foreach (var match in classificationDTO.LastMatches)
    {
        var result = new LastResultsDTO();

        if(match.HomeWinnigSets == 3)
        {
            if(match.HomeId == classificationDTO.TeamId)
            {
                result.Won = true;
            }
            else
            {
                result.Lose = true;
            } 
        }
        else if(match.VisitorWinnigSets == 3)
        {
            if(match.VisitorId == classificationDTO.TeamId)
            {
                result.Won = true;
            }
            else
            {
                result.Lose = true;
            } 
        }
        results.Add(result);
    }
    return results;
   }
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
    private async Task<int> GetLastSet(int matchId)
        => await _dbService.GetAsync<int>("SELECT MAX(Set) from goals where MatchId = @matchId", new {matchId});
    private async Task<bool> IsItFirstSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM goals WHERE MatchId = @matchId);", new {matchId});
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
            @"SELECT COALESCE(SUM(TotalGoals), 0) AS GrandTotalGoals
            FROM (
                SELECT g.TeamId, COUNT(g.Id) AS TotalGoals
                FROM Goals g
                JOIN Matches m ON g.MatchId = m.Id
                WHERE m.ChampionshipId = @championshipId AND
                    (m.Visitor = @teamId OR m.Home = @teamId) AND 
                    (g.TeamId <> @teamId AND g.OwnGoal = false OR g.TeamId = @teamId AND g.OwnGoal = true)
                GROUP BY g.TeamId
            ) AS SubqueryAlias;",
            new { championshipId, teamId });
    
    public async Task<List<StrikerDTO>> GetStrikersValidationAsync(int championshipId)
    {
        var championship = await GetChampionshipByIdSend(championshipId);

        if(championship is null)
            throw new ApplicationException("Campeonato não existe");
        
        var strikers = new List<StrikerDTO>();
        
        var players = await _dbService.GetAll<PlayerGoalsSummaryDTO>(
                @"SELECT COALESCE(PlayerId, PlayerTempId) AS PlayerIdOrTempId, COUNT(*) AS Goals
                FROM goals
                GROUP BY COALESCE(PlayerId, PlayerTempId)
                ORDER BY Goals DESC
                LIMIT 15;", 
                new {championshipId});
        
        players = players.OrderByDescending(r => r.Goals).ToList();

        int distinctCount = 0;
        int fifthLargestGoals = 0;

        for (int i = 0; i < players.Count; i++)
        {
            if (i == 0 || players[i].Goals != players[i - 1].Goals)
            {
                distinctCount++;
            }

            if (distinctCount == 5)
            {
                fifthLargestGoals = players[i].Goals;
                break;
            }
        }
        
        players.RemoveAll(p => p.Goals < fifthLargestGoals);

        foreach (var player in players)
        {
            var user = await _dbService.GetAsync<User>("SELECT * FROM users WHERE Id = @id", new {id = player.PlayerIdOrTempId});
            if(user is not null)
            {
                var team = await GetByTeamIdSendAsync(user.PlayerTeamId);
                var striker = new StrikerDTO();
                striker.Goals = player.Goals;
                striker.Name = user.Name;
                striker.Picture = user.Name;
                striker.TeamEmblem = team.Emblem;
                strikers.Add(striker);
            }
            else
            {
                var playerTemp = await _dbService.GetAsync<PlayerTempProfile>("SELECT * FROM playertempprofiles WHERE Id = @id", new {id = player.PlayerIdOrTempId});
                var team = await GetByTeamIdSendAsync(playerTemp.TeamsId);
                var striker = new StrikerDTO();
                striker.Goals = player.Goals;
                striker.Name = playerTemp.Name;
                striker.Picture = playerTemp.Picture;
                striker.TeamEmblem = team.Emblem;
                strikers.Add(striker);
            }
        }
        return strikers;
    }
}