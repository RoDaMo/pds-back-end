
using FluentValidation;
using PlayOffsApi.Enum;
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

    public async Task EndGameValidationAsync(int matchId)
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
        var id = await _dbService.EditData("UPDATE matches SET Winner = @teamId WHERE id = @matchId returning id", new {teamId, matchId});
        var match = await GetMatchById(matchId);
        if(await CheckIfMatchesOfCurrentPhaseHaveEnded(match.ChampionshipId, match.Phase) && match.Phase != Phase.Finals)
        {
            var matches = await _dbService.GetAll<Match>("SELECT * from matches WHERE ChampionshipId = @championshipId AND Phase = @phase", new {match.ChampionshipId, match.Phase});
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
}