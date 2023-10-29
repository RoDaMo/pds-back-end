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
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM playertempprofiles WHERE id = @playerTempId AND TeamsId = @teamId AND accepted = true);", new {playerTempId, teamId});
    private async Task<bool> CheckRelationshipBetweenPlayerAndTeam(Guid playerId, int teamId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM users WHERE id = @playerId AND PlayerTeamId = @teamId AND accepted = true);", new {playerId, teamId});
    private async Task<bool> CheckRelationshipBetweenTeamAndMatch(int teamId, int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND (home = @teamId OR visitor = @teamId) );", new {matchId, teamId});
    private async Task<bool> DepartureDateNotSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date IS NULL);", new {matchId});
    // private async Task<bool> DidMatchNotStart(int matchId)
    //     => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date <> CURRENT_DATE);", new {matchId});
    private async Task<Championship> GetChampionshipByMatchId(int matchId)
        => await _dbService.GetAsync<Championship>(
            @"SELECT c.*
            FROM Championships c
            JOIN Matches m ON c.Id = m.ChampionshipId
            WHERE m.Id = @matchId;
            ", 
            new {matchId});
    private async Task<int> GetPointsFromTeamByIdInTwoMatches(int matchId, int teamId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(*) FROM goals 
            WHERE (MatchId = @matchId OR MatchId = (SELECT Id FROM matches WHERE PreviousMatch = @matchId) OR MatchId =  (SELECT PreviousMatch FROM matches WHERE id = @matchId)) AND 
            (TeamId = @teamId AND OwnGoal = false OR TeamId <> @teamId AND OwnGoal = true)",
        new {matchId, teamId});
    private async Task<bool> CheckIfFirstMatchHasNotFinished(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND (WINNER IS NULL AND Tied = false))", new {matchId});
    private async Task<bool> AreTeamsTied(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);

        if(match is null)
           return false;

        if(championship.SportsId == Sports.Volleyball)
            return false;
        
        if(championship.Format == Format.LeagueSystem)
            return false;
        
        if(championship.Format == Format.GroupStage && match.Round != 0)
           return false;

        if(match.Phase != Phase.Finals && championship.DoubleMatchEliminations ||
            match.Phase == Phase.Finals && championship.FinalDoubleMatch)
        {
            var aggregateVisitorPoints = await GetPointsFromTeamByIdInTwoMatches(match.Id, match.Visitor);
            var aggregateHomePoints = await GetPointsFromTeamByIdInTwoMatches(match.Id, match.Home);
            if(match.PreviousMatch == 0)
                return false;
            else if(await CheckIfFirstMatchHasNotFinished(match.PreviousMatch))
                return false;
            else if(aggregateHomePoints != aggregateVisitorPoints)
            return false;
        }

        else
        {
            var visitorPoints = await GetPointsFromTeamById(matchId, match.Visitor);
            var homePoints = await GetPointsFromTeamById(matchId, match.Home);

            if(visitorPoints != homePoints)
                return false;
        }
        return true;
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
    // private async Task<int> DefineWinner(int teamId, int matchId)
    // {
    //     var id = await _dbService.EditData("UPDATE matches SET Winner = @teamId WHERE id = @matchId returning id", new {teamId, matchId});
    //     var match = await GetMatchById(matchId);
    //     if(await CheckIfMatchesOfCurrentPhaseHaveEnded(match.ChampionshipId, match.Phase) && match.Phase != Phase.Finals)
    //     {
    //         var matches = await _dbService.GetAll<Match>("SELECT * from matches WHERE ChampionshipId = @championshipId AND Phase = @phase", new {match.ChampionshipId, match.Phase});
    //         var newPhase = match.Phase + 1;
    //         for (int i = 0; i <= matches.Count() / 2; i = i + 2)
    //         {
    //             var newMatch = new Match(match.ChampionshipId, matches[i].Winner, matches[i+1].Winner, newPhase);
    //             await CreateMatchSend(newMatch);
    //         }

    //     }
    //     return id;
    // }
    private async Task<int> DefineWinner(int teamId, int matchId)
    {
        var id = await UpdateMatchToDefineWinner(teamId, matchId);
        var match2 = await GetMatchById(matchId);
        var championship2 = await GetChampionshipByMatchId(matchId);
        if(await CheckIfMatchesOfCurrentPhaseHaveEnded(match2.ChampionshipId, match2.Phase) && match2.Phase != Phase.Finals)
        {
            var newPhase = match2.Phase + 1;

            if((newPhase != Phase.Finals && championship2.DoubleMatchEliminations) || (newPhase == Phase.Finals && championship2.FinalDoubleMatch))
		    {
                var winners = await GetWinners(championship2, match2.Phase); 
                for (int i = 0; i <= winners.Count()-2; i = i + 2)
                {
                    var newMatch = new Match(match2.ChampionshipId, winners[i], winners[i+1], newPhase);
                    var previousMatch = await CreateMatchSend(newMatch);
                    var newMatch2 = new Match(match2.ChampionshipId, winners[i+1],  winners[i], newPhase, previousMatch.Id);
                    await CreateMatchSend2(newMatch2);
                }
            }

            else
            {
                var winners = await GetWinners(championship2, match2.Phase); 
                for (int i = 0; i <= winners.Count()-2; i = i + 2)
                {
                    var newMatch = new Match(match2.ChampionshipId, winners[i], winners[i+1], newPhase);
                    await CreateMatchSend(newMatch);
                }
            }
        }
        return id;
    }
    private async Task<Match> CreateMatchSend2(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase, Round, PreviousMatch) VALUES(@ChampionshipId, @Home, @Visitor, @Phase, @Round, @PreviousMatch) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
    private async Task<List<int>> GetWinners(Championship championship, Phase phase)
    {
        var winners = new List<int>();
        var aux = 1;
        var matches = await _dbService.GetAll<Match>("SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Phase = @phase ORDER BY Id", new {championshipId = championship.Id, phase});
        if((championship.DoubleMatchEliminations && phase != Phase.Finals) || (championship.FinalDoubleMatch && phase == Phase.Finals))
            aux = 2;

        for (int i = 0; i < matches.Count(); i = i + aux)
        {
            if(!matches[i].Tied)
            {
                var aggregateVisitorPoints = await GetPointsFromTeamByIdInTwoMatches(matches[i].Id, matches[i].Visitor);
                var aggregateHomePoints = await GetPointsFromTeamByIdInTwoMatches(matches[i].Id, matches[i].Home);
                if(aggregateVisitorPoints > aggregateHomePoints)
                {
                    winners.Add(matches[i].Visitor);
                }
                else if(aggregateVisitorPoints < aggregateHomePoints)
                {
                    winners.Add(matches[i].Home);
                }
                else
                {
                    winners.Add(matches[i].Winner);
                }
            }

            else
            {
                winners.Add(matches[i+1].Winner);
            }
        }
        return winners;
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
        var numberOfPlayersOnTeam = await _dbService.GetAsync<int>("SELECT COUNT(*) FROM playertempprofiles WHERE TeamsId = @teamId AND accepted = true", new {teamId});
        numberOfPlayersOnTeam = numberOfPlayersOnTeam + await _dbService.GetAsync<int>("SELECT COUNT(*) FROM users WHERE PlayerTeamId = @teamId AND accepted = true", new {teamId});
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
        var numberOfPlayersOnTeam = await _dbService.GetAsync<int>("SELECT COUNT(*) FROM users WHERE PlayerTeamId = @teamId AND accepted = true", new {teamId});
        numberOfPlayersOnTeam = numberOfPlayersOnTeam + await _dbService.GetAsync<int>("SELECT COUNT(*) FROM playertempprofiles WHERE TeamsId = @teamId AND accepted = true", new {teamId});
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
    
        