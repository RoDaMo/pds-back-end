using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;


public class FoulService
{
    private readonly DbService _dbService;
    private readonly GoalService _goalService;
    private readonly MatchService _matchService;
    public FoulService(DbService dbService, GoalService goalService, MatchService matchService)
    {
        _dbService = dbService;
        _goalService = goalService;  
        _matchService = matchService;
    }

    public async Task<List<string>> CreateFoulValidationAsync(Foul foul)
    {
        var foulValidation = new FoulValidation();
		var result = await foulValidation.ValidateAsync(foul);

        if (!result.IsValid)
		{
			return result.Errors.Select(x => x.ErrorMessage).ToList();
		}

        var match = await GetMatchById(foul.MatchId);
        var championship = await GetChampionshipByMatchId(foul.MatchId);
        var player = await GetUserById(foul.PlayerId);
        var playerTemp = await GetPlayerTempProfileById(foul.PlayerTempId);

        if(match is null)
            throw new ApplicationException("Partida passada não existe");
        
        if(match.Date >= DateTime.UtcNow)
            throw new ApplicationException("Partida ainda não iniciou");
        
        if(match.HomeUniform is null || match.VisitorUniform is null)
            throw new ApplicationException("É necessário definir os uniformes das equipes antes");
        
        if (match.Road is null)
            throw new ApplicationException("É necessário definir o local da partida antes antes");
        
        if(match.Winner != 0 || match.Tied == true)
            throw new ApplicationException("Partida já foi finalizada");
        
        if(player is null && playerTemp is null)
            throw new ApplicationException("Jogador passado não existe");
        
        if(championship.SportsId == Sports.Volleyball)
            throw new ApplicationException("Faltas estão indisponíveis para vôlei");
        
        if(await CheckIfMinutesIsNotValid(foul, match))
            throw new ApplicationException("Tempo do evento é inválido");
        
        if(playerTemp is null)
        {
            var fouls = await GetAllFoulsByUserAndMatch(player.Id, match.Id);
            var numberOfYellowCards = fouls.Count(f => f.YellowCard == true);
            if(numberOfYellowCards == 1 && foul.YellowCard)
            {
                foul.Considered = false;
                foul.Valid = false;
                await CreateFoulToPlayerSend(foul);
                foul.Considered = true;
                foul.YellowCard = false;
                foul.Valid = true;
                await CreateFoulToPlayerSend(foul);
            }

            else
            {
                foul.Considered = true;
                foul.Valid = true;
                await CreateFoulToPlayerSend(foul);
            }

            if(await CheckIfRedCardsOfTeamIsEqualFive(match.Id, player.PlayerTeamId))
            {
                await _goalService.RemoveAllGoalOfMatchValidation(match.Id);
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerId = player.Id;
                    goal.Minutes = foul.Minutes;
                    goal.OwnGoal = true;
                    await _goalService.CreateGoalValidationAsync(goal);
                }

                if(match.Round != 0 && championship.Format == Enum.Format.GroupStage)
                    await _matchService.EndGameToGroupStageValidationAsync(match.Id);
                else if(match.Round != 0 && championship.Format == Enum.Format.LeagueSystem)
                    await _matchService.EndGameToLeagueSystemValidationAsync(match.Id);
                else
                    await _matchService.EndGameToLeagueSystemValidationAsync(match.Id);
            }
        }
        else
        {
            var fouls = await GetAllFoulsByPlayerTempAndMatch(playerTemp.Id, match.Id);
            var numberOfYellowCards = fouls.Count(f => f.YellowCard == true);
            if(numberOfYellowCards == 1 && foul.YellowCard)
            {
                foul.Considered = false;
                foul.Valid = false;
                await CreateFoulToPlayerTempSend(foul);
                foul.Considered = true;
                foul.YellowCard = false;
                foul.Valid = true;
                await CreateFoulToPlayerTempSend(foul);
            }
            else
            {
                foul.Considered = true;
                foul.Valid = true;
                await CreateFoulToPlayerTempSend(foul);
            }

            if(await CheckIfRedCardsOfTeamIsEqualFive(match.Id, playerTemp.TeamsId))
            {
                await _goalService.RemoveAllGoalOfMatchValidation(match.Id);
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerTempId = playerTemp.Id;
                    goal.Minutes = foul.Minutes;
                    goal.OwnGoal = true;
                    await _goalService.CreateGoalValidationAsync(goal);
                }

                if(match.Round != 0 && championship.Format == Enum.Format.GroupStage)
                    await _matchService.EndGameToGroupStageValidationAsync(match.Id);
                else if(match.Round != 0 && championship.Format == Enum.Format.LeagueSystem)
                    await _matchService.EndGameToLeagueSystemValidationAsync(match.Id);
                else
                    await _matchService.EndGameToLeagueSystemValidationAsync(match.Id);
            }           
        }
        return new();
    }
    private async Task<bool> CheckIfRedCardsOfTeamIsEqualFive(int matchId, int teamId)
    {
         var tempCards = await _dbService.GetAsync<int>(
            @"SELECT COUNT(*)
            FROM Fouls f
            JOIN PlayerTempProfiles p ON f.PlayerTempId = p.Id
            WHERE p.TeamsId = @teamId AND f.YellowCard = false AND MatchId = @matchId;", 
            new {teamId, matchId});
        var userCards = await _dbService.GetAsync<int>(
            @"SELECT COUNT(*)
            FROM Fouls f
            JOIN Users u ON f.PlayerId = u.Id
            WHERE u.PlayerTeamId = @teamId AND MatchId = @matchId AND f.YellowCard = false;", 
            new {teamId, matchId});
        return (tempCards + userCards == 5) ? true : false;
    }
        
    private async Task<Match> GetMatchById(int matchId)
        => await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @matchId", new{matchId});
    private async Task<Championship> GetChampionshipByMatchId(int matchId)
        => await _dbService.GetAsync<Championship>(
            @"SELECT c.*
            FROM Championships c
            JOIN Matches m ON c.Id = m.ChampionshipId
            WHERE m.Id = @matchId;
            ", 
            new {matchId});
    private async Task<User> GetUserById(Guid id)
        => await _dbService.GetAsync<User>(@"SELECT * FROM users WHERE Id = @id", new {id});
    private async Task<PlayerTempProfile> GetPlayerTempProfileById(Guid id)
        => await _dbService.GetAsync<PlayerTempProfile>(@"SELECT * FROM playertempprofiles WHERE Id = @id", new {id});
    private async Task<List<Foul>> GetAllFoulsByUserAndMatch(Guid userId, int matchId)
        => await _dbService.GetAll<Foul>("SELECT * FROM fouls WHERE PlayerId = @userId AND MatchId = @matchId", new {userId, matchId});
    private async Task<List<Foul>> GetAllFoulsByPlayerTempAndMatch(Guid playerTempId, int matchId)
        => await _dbService.GetAll<Foul>("SELECT * FROM fouls WHERE PlayerTempId = @playerTempId AND MatchId = @matchId", new {playerTempId, matchId});
    private async Task<bool> CheckIfMinutesIsNotValid(Foul foul, Match math)
    {
        var lastEventGoal = await GetTimeOfLastEventGoal(foul.MatchId);
        var lastEventFoul = await GetTimeOfLastEventFoul(foul.MatchId);
        if(math.Prorrogation)
        {
            if(lastEventFoul > lastEventGoal)
            {
                if(foul.Minutes > 120 || lastEventFoul > foul.Minutes || foul.Minutes < 90)
                    return true;
                return false;
            }
            else
            {
                    if(foul.Minutes > 120 || lastEventGoal > foul.Minutes || foul.Minutes < 90)
                    return true;
                return false;
            }
            
        }
        else if(lastEventFoul > lastEventGoal)
        {
            if(foul.Minutes > 90 || lastEventFoul > foul.Minutes)
            {
                return true;
            }
            return false;
        }

        else
        {
            if(foul.Minutes > 90 || lastEventGoal > foul.Minutes)
            {
                return true;
            }
        }
        return false;
        
    }
    private async Task<int> GetTimeOfLastEventGoal(int matchId)
        => await _dbService.GetAsync<int>("SELECT Minutes FROM Goals WHERE MatchId = @matchId ORDER BY Id DESC LIMIT 1", new {matchId});
    private async Task<int> GetTimeOfLastEventFoul(int matchId)
        => await _dbService.GetAsync<int>("SELECT Minutes FROM Fouls WHERE MatchId = @matchId ORDER BY Id DESC LIMIT 1", new {matchId});
    
    
    private async Task CreateFoulToPlayerSend(Foul foul)
    {
        await _dbService.EditData("INSERT INTO Fouls (YellowCard, Considered, MatchId, PlayerId, Minutes, Valid) VALUES (@YellowCard, @Considered, @MatchId, @PlayerId, @Minutes, @Valid)", foul);
    }
    private async Task CreateFoulToPlayerTempSend(Foul foul)
    {
        await _dbService.EditData("INSERT INTO Fouls (yellowcard, considered, matchid, playertempid, minutes, valid) VALUES (@YellowCard, @Considered, @MatchId, @PlayerTempId, @Minutes, @Valid)", foul);
    }
    
}