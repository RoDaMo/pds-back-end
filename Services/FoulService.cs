using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;


public class FoulService
{
    private readonly DbService _dbService;
    public FoulService(DbService dbService)
    {
        _dbService = dbService;   
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
        
        if (match.Local is null)
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

            foul.Considered = true;
            foul.Valid = true;
            await CreateFoulToPlayerSend(foul);

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
        }
        return new();
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
    private async Task<User> GetPlayerTempProfileById(Guid id)
        => await _dbService.GetAsync<User>(@"SELECT * FROM playertempprofiles WHERE Id = @id", new {id});
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