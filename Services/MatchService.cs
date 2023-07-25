
using FluentValidation;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;

public class MatchService
{
    private readonly DbService _dbService;
    public MatchService(DbService dbService)
    {
        _dbService = dbService;
    }

    public async Task<List<string>> CreateGoalValidationAsync(Goal goal)
    {
        var errorMessages = new List<string>();
        var goalValidator = new GoalValidator();
        var sportId = await GetSportByTeamId(goal.TeamId);
        
		var result = (sportId == 1) 
        ? goalValidator.Validate(goal, options => options.IncludeRuleSets("ValidationSoccer"))
        : goalValidator.Validate(goal, options => options.IncludeRuleSets("ValidationVolleyBall"));

		if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

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

        if(await DidMatchNotStart(goal.MatchId))
        {
            throw new ApplicationException("Partida ainda não inciada ou já encerrada.");
        }

        if(sportId == 1)
        {
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

            await EndGame(goal.MatchId, goal.TeamId);
        }
        return errorMessages;
    }
    private async Task<bool> CheckRelationshipBetweenTeamAndMatch(int teamId, int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND (home = @teamId OR visitor = @teamId) );", new {matchId, teamId});
    private async Task<bool> CheckRelationshipBetweenPlayerAndTeam(Guid playerId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE id = @playerId AND PlayerTeamId = @teamId);", new {playerId, teamId});
    private async Task<bool> CheckRelationshipBetweenPlayerTempAndTeam(Guid playerTempId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM playertempprofiles WHERE id = @playerTempId AND TeamsId = @teamId);", new {playerTempId, teamId});
    private async Task<bool> DidMatchNotStart(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date <> CURRENT_DATE);", new {matchId});
    private async Task<bool> SetIsInvalid(int matchId, int teamId, int set)
    {
        var lastSet = 0;
        lastSet = (!(await IsItFirstSet(matchId))) ? 1 : await GetLastSet(matchId);
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
    private async Task<bool> ThereIsAWinner(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND winner IS NOT NULL);", new {matchId});

    private async Task EndGame(int matchId, int teamId)
    {
        var pointsForSet = new List<int>();
        var pointsForSet2 = new List<int>();
        var WonSets = 0;
        var WonSets2 = 0;
        var lastSet = 0;
        lastSet = (!(await IsItFirstSet(matchId))) ? 1 : await GetLastSet(matchId);
        var team2Id = await _dbService.GetAsync<int>("SELECT home, visitor, CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches;", new {teamId});

        for (int i = 0;  i < lastSet; i++)
        {
            pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND TeamId = @teamId AND Set = @j", new {matchId, teamId, j = i+1}));
            pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND TeamId <> @teamId AND Set = @j", new {matchId, teamId, j = i+1}));
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

        if(WonSets == 3)
        {
            await DefineWinner(teamId, matchId);
        }
        else if(WonSets2 == 3)
        {
            await DefineWinner(team2Id, matchId);
        }
        
    }
    private async Task<int> DefineWinner(int teamId, int matchId)
        => await _dbService.EditData("UPDATE matches SET Winner = @teamId WHERE id = @matchId returning id", new {teamId, matchId});

    private async Task<bool> IsItFirstSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM goals WHERE MatchId = @matchId);", new {matchId});

    private async Task<int> GetLastSet(int matchId)
        => await _dbService.GetAsync<int>("SELECT MAX(Set) from goals where MatchId = @matchId", new {matchId});
    private async Task<int> GetSportByTeamId(int teamId)
        => await _dbService.GetAsync<int>("SELECT SportsId FROM teams where id = @teamId", new {teamId});
    private async Task<int> CreateGoalToPlayerTempSend(Goal goal)
        => await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal) VALUES (@MatchId, @TeamId, null, @PlayerTempId, @Set, @OwnGoal) RETURNING Id;",
			goal);
    private async Task<int> CreateGoalToPlayerSend(Goal goal)
        => await _dbService.EditData(
			"INSERT INTO goals (MatchId, TeamId, PlayerId, PlayerTempId, Set, OwnGoal) VALUES (@MatchId, @TeamId, @PlayerId, null, @Set, @OwnGoal) RETURNING Id;",
			goal);
}