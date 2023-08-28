using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;

public class PenaltyService
{
    private readonly DbService _dbService;
    public PenaltyService(DbService dbService)
    {
        _dbService = dbService;
    }

    public async Task<List<string>> CreatePenaltyValidationAsync(Penalty penalty)
    {
        var errorMessages = new List<string>();
        var penaltyValidator = new PenaltyValidator();
        var result = await penaltyValidator.ValidateAsync(penalty);

        if (!result.IsValid)
		{
			errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
			return errorMessages;
		}

        if(penalty.PlayerId == Guid.Empty)
        {
            if(!await CheckRelationshipBetweenPlayerTempAndTeam(penalty.PlayerTempId, penalty.TeamId))
            {
                throw new ApplicationException("Jogador não pertence ao time informado.");
            }
        }
        else
        {
            if(!await CheckRelationshipBetweenPlayerAndTeam(penalty.PlayerId, penalty.TeamId))
            {
                throw new ApplicationException("Jogador não pertence ao time informado.");
            }
        }

        if(!await CheckRelationshipBetweenTeamAndMatch(penalty.TeamId, penalty.MatchId))
        {
            throw new ApplicationException("Time informado não participa da partida atual.");
        }

        if(await DepartureDateNotSet(penalty.MatchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }

        // if(await DidMatchNotStart(penalty.MatchId))
        // {
        //     throw new ApplicationException("Partida ainda não inciada ou já encerrada.");
        // }

        if(!await AreTeamsTied(penalty.MatchId))
        {
            throw new ApplicationException("Pênaltis apenas para times empatados.");
        }

        if(await CheckIfThereIsWinner(penalty.MatchId))
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }

        if(await CheckLastPenaltyWasNotTakenByThisTeam(penalty.TeamId, penalty.MatchId))
        {
            throw new ApplicationException("O mesmo time não pode cobrar dois pênatis em sequência.");
        }

        if(penalty.PlayerId == Guid.Empty)
        {

            if(await CheckIfPlayerTempHasAlreadyTakenPenalty(penalty.PlayerTempId, penalty.TeamId, penalty.MatchId))
            {
                throw new ApplicationException("Jogador passado já cobrou um pênalti.");
            }
            await CreatePenaltyToPlayerTempSend(penalty);
        }
        else
        {
            if(await CheckIfPlayerHasAlreadyTakenPenalty(penalty.PlayerId, penalty.TeamId ,penalty.MatchId))
            {
                throw new ApplicationException("Jogador passado já cobrou um pênalti.");
            }
            await CreatePenaltyToPlayerSend(penalty);
        }

        var match = await GetMatchById(penalty.MatchId);
        var homeTeamPenalties = await GetPenaltiesByTeamIdAndMatchId(match.Home, match.Id);
        var visitorTeamPenalties = await GetPenaltiesByTeamIdAndMatchId(match.Visitor, match.Id);

        if(homeTeamPenalties.Count() + visitorTeamPenalties.Count() <= 10)
        {
            var numberOfGoalsScoredByHome = homeTeamPenalties.Count(p => p.Converted);
            var numberOfGoalsScoredByVisitor = visitorTeamPenalties.Count(p => p.Converted);
            var homeValor = 5 - homeTeamPenalties.Count() + numberOfGoalsScoredByHome;
            var visitorValor = 5 - visitorTeamPenalties.Count() + numberOfGoalsScoredByVisitor;

            if(numberOfGoalsScoredByHome > visitorValor)
            {
                await DefineWinner(match.Home, match.Id);
            }

            else if(numberOfGoalsScoredByVisitor > homeValor)
            {
                await DefineWinner(match.Visitor, match.Id);
            }

        }
        else if(homeTeamPenalties.Count() + visitorTeamPenalties.Count() >= 12)
        {
            if(await CheckIfLastPenaltyWasConvertedByTeamIdAndMatchId(match.Home, match.Id) && 
                !(await CheckIfLastPenaltyWasConvertedByTeamIdAndMatchId(match.Visitor, match.Id)))
            {
                await DefineWinner(match.Home, match.Id);
            }
            else if(await CheckIfLastPenaltyWasConvertedByTeamIdAndMatchId(match.Visitor, match.Id) && 
                !(await CheckIfLastPenaltyWasConvertedByTeamIdAndMatchId(match.Home, match.Id)))
            {
                await DefineWinner(match.Visitor, match.Id);
            }
        }
        return errorMessages;
    }
    private async Task<bool> CheckRelationshipBetweenPlayerTempAndTeam(Guid playerTempId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM playertempprofiles WHERE id = @playerTempId AND TeamsId = @teamId);", new {playerTempId, teamId});
    private async Task<bool> CheckRelationshipBetweenPlayerAndTeam(Guid playerId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE id = @playerId AND PlayerTeamId = @teamId);", new {playerId, teamId});
    private async Task<bool> CheckRelationshipBetweenTeamAndMatch(int teamId, int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND (home = @teamId OR visitor = @teamId) );", new {matchId, teamId});
    private async Task<bool> DepartureDateNotSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date IS NULL);", new {matchId});
    // private async Task<bool> DidMatchNotStart(int matchId)
    //     => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date <> CURRENT_DATE);", new {matchId});
    private async Task<bool> AreTeamsTied(int matchId)
    {
        var match = await GetMatchById(matchId);
        var visitorPoints = await GetPointsFromTeamById(matchId, match.Visitor);
        var homePoints = await GetPointsFromTeamById(matchId, match.Home);
        if(homePoints == visitorPoints)
        {
            return true;
        }
        return false;
    }
    private async Task<Match> GetMatchById(int matchId)
        => await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @matchId", new{matchId});
    private async Task<int> GetPointsFromTeamById(int matchId, int teamId)
        => await _dbService.GetAsync<int>("SELECT COUNT(*) FROM goals WHERE MatchId = @matchId AND (TeamId = @teamId AND OwnGoal = false OR TeamId <> @teamId AND OwnGoal = true)", new {matchId, teamId});
    private async Task<bool> CheckIfThereIsWinner(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND Winner IS NOT NULL)", new {matchId});
    private async Task<int> CreatePenaltyToPlayerTempSend(Penalty penalty)
        => await _dbService.EditData(
			"INSERT INTO penalties (MatchId, TeamId, PlayerId, PlayerTempId, Converted) VALUES (@MatchId, @TeamId, null, @PlayerTempId, @Converted) RETURNING Id;",
			penalty);
    private async Task<int> CreatePenaltyToPlayerSend(Penalty penalty)
        => await _dbService.EditData(
			"INSERT INTO penalties (MatchId, TeamId, PlayerId, PlayerTempId, Converted) VALUES (@MatchId, @TeamId, @PlayerId, null, @Converted) RETURNING Id;",
			penalty);
    private async Task<List<Penalty>> GetPenaltiesByTeamIdAndMatchId(int teamId, int matchId)
        => await _dbService.GetAll<Penalty>("SELECT * FROM penalties WHERE TeamId = @teamId AND MatchId = @matchId;", new {teamId, matchId});
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
    private async Task<bool> CheckIfLastPenaltyWasConvertedByTeamIdAndMatchId(int teamId, int matchId)
    {
        var lastPenalty = await _dbService.GetAsync<Penalty>("SELECT * FROM penalties WHERE TeamId = @teamId AND MatchId = @matchId ORDER BY Id DESC LIMIT 1", new {teamId, matchId});
        if(lastPenalty.Converted)
        {
            return true;
        }  
        return false;
    }
    private async Task<bool> CheckLastPenaltyWasNotTakenByThisTeam(int teamId, int matchId)
    {
        var lastPenalty = await _dbService.GetAsync<Penalty>("SELECT * FROM penalties WHERE MatchId = @matchId ORDER BY Id DESC LIMIT 1", new {matchId});
        if(lastPenalty == null) 
            return false;
        if(lastPenalty.TeamId == teamId)
        {
            return true;
        }
        return false;
    }

    private async Task<bool> CheckIfPlayerTempHasAlreadyTakenPenalty(Guid playerTempId, int teamId, int matchId)
    {
        //quantidade de jogadores vai mudar em função dos cartões
        var numberOfPlayersOnTeam = await _dbService.GetAsync<int>("SELECT COUNT(*) FROM playertempprofiles WHERE TeamsId = @teamId", new {teamId});
        numberOfPlayersOnTeam = numberOfPlayersOnTeam + await _dbService.GetAsync<int>("SELECT COUNT(*) FROM users WHERE PlayerTeamId = @teamId", new {teamId});
        var penalties = await _dbService.GetAll<Penalty>("SELECT * FROM penalties WHERE MatchId = @matchId AND TeamId = @teamId ORDER BY Id", new {matchId, teamId});
        var quotient = (int)(penalties.Count() / numberOfPlayersOnTeam);
        quotient = quotient * numberOfPlayersOnTeam;
        var rest = penalties.Count() % numberOfPlayersOnTeam;

        for(int i = quotient; i < rest + quotient; i++)
        {
            if(penalties[i].PlayerTempId == playerTempId)
            {
                return true;
            } 
        }
        return false;
    }
    private async Task<bool> CheckIfPlayerHasAlreadyTakenPenalty(Guid playerId, int teamId, int matchId)
    {
        var numberOfPlayersOnTeam = await _dbService.GetAsync<int>("SELECT COUNT(*) FROM users WHERE PlayerTeamId = @teamId", new {teamId});
        numberOfPlayersOnTeam = numberOfPlayersOnTeam + await _dbService.GetAsync<int>("SELECT COUNT(*) FROM playertempprofiles WHERE TeamsId = @teamId", new {teamId});
        var penalties = await _dbService.GetAll<Penalty>("SELECT * FROM penalties WHERE MatchId = @matchId AND TeamId = @teamId ORDER BY Id", new {matchId, teamId});
        var quotient = (int)(penalties.Count() / numberOfPlayersOnTeam);
        quotient = quotient * numberOfPlayersOnTeam;
        var rest = penalties.Count() % numberOfPlayersOnTeam;

        for(int i = quotient; i < rest + quotient; i++)
        {
            if(penalties[i].PlayerId == playerId)
            {
                return true;
            } 
        }
        return false;
    }

}
    
        