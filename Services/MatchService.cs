
using FluentValidation;
using Microsoft.AspNetCore.Routing.Tree;
using Microsoft.Net.Http.Headers;
using PlayOffsApi.DTO;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;

namespace PlayOffsApi.Services;

public class MatchService
{
    private readonly DbService _dbService;
    private readonly BracketingService _bracketingService;
    private readonly GoalService _goalService;

    public MatchService(DbService dbService, BracketingService bracketingService, GoalService goalService)
    {
        _dbService = dbService;
        _bracketingService = bracketingService;
        _goalService = goalService;
    }

    public async Task EndGameToKnockoutValidationAsync(int matchId)
    {
        var match = await GetMatchById(matchId);
        if(match is null)
        {
            throw new ApplicationException("Partida passada não existe.");
        }
        if(await DepartureDateNotSet(matchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }
        if(match.Date.ToUniversalTime() >= DateTime.UtcNow)
        {
            throw new ApplicationException("Partida ainda não inciou");
        }
        if(match.Winner != 0)
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }

        var championship = await GetByIdSend(match.ChampionshipId);

        if(match.HomeUniform is null || match.VisitorUniform is null)
            throw new ApplicationException("É necessário definir os uniformes das equipes antes");
        
        if (match.Road is null)
            throw new ApplicationException("É necessário definir o local da partida antes antes");

        if(championship.DoubleMatchEliminations && match.Phase != Phase.Finals || 
        championship.FinalDoubleMatch && match.Phase == Phase.Finals)
		{
            var aggregateVisitorPoints = await GetPointsFromTeamByIdInTwoMatches(matchId, match.Visitor);
            var aggregateHomePoints = await GetPointsFromTeamByIdInTwoMatches(matchId, match.Home);
            if(match.PreviousMatch != 0)
            {
                if(aggregateVisitorPoints == aggregateHomePoints && match.Winner == 0)
                {
                    throw new ApplicationException("Partida não pode ser encerrada sem um vencedor nos gols agregados.");
                }
                if(await CheckIfFirstMatchHasNotFinished(match.PreviousMatch))
                {
                    throw new ApplicationException("A primeira partida deve ser finalizada antes.");
                }
            }
            if(aggregateHomePoints > aggregateVisitorPoints)
            {
                await DefineWinner(match.Home, matchId);
            }
            else if(aggregateHomePoints < aggregateVisitorPoints)
            {
                await DefineWinner(match.Visitor, matchId);
            } 
            else 
            {
                await DefineWinner(0, matchId);
            }
        }

        else
		{
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

        var users = await GetAllUsersByTeamsId(match.Home, match.Visitor);
        var tempUsers = await GetAllTempProfileByTeamsId(match.Home, match.Visitor);

        foreach (var user in users)
        {
            await InvalidingUserCards(user, championship, match);
        }

        foreach (var temp in tempUsers)
        {
            await InvalidingPlayerTempCards(temp, championship, match);
        }
    }
    private async Task<List<User>> GetAllUsersByTeamsId(int id1, int id2)
        => await _dbService.GetAll<User>("SELECT * FROM Users WHERE PlayerTeamId = @id1 OR PlayerTeamId = @id2", new {id1, id2});
    private async Task<List<PlayerTempProfile>> GetAllTempProfileByTeamsId(int id1, int id2)
        =>  await _dbService.GetAll<PlayerTempProfile>("SELECT * FROM PlayerTempProfiles WHERE TeamsId = @id1 OR TeamsId = @id2", new {id1, id2});
    private async Task InvalidingUserCards(User user, Championship championship, Match match)
    {
        if(match.Phase != 0)
        {
            var redCard = await GetRedCardValid(championship.Id, user.Id);
            var isNextMatch = false;
            if(redCard is not null)
            {
                var previousMatch = await GetMatchById(redCard.MatchId);
                isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch, match, user);

                if(isNextMatch)
                {
                    await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", new {id = redCard.Id});
                    return;
                }
            }
              
            var yellowCards = await GetLastYellowCardsValid(championship.Id, user.Id);
            if(yellowCards.Count != 0)
            {
                var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch2, match, user);
                if(yellowCards.Count == 3 && isNextMatch)
                {
                    foreach (var YellowCard in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", 
                            new {id = YellowCard.Id});
                    }
                    return;
                }
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch(yellowCards[0].MatchId, user.Id))
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
            }
        }

        else if(championship.Format == Enum.Format.GroupStage)
        {
            var redCard = await GetRedCardValid(championship.Id, user.Id);
            var isNextMatch = false;
            if(redCard is not null)
            {
                var previousMatch = await GetMatchById(redCard.MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, user, 1);

                if(isNextMatch)
                {
                    await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", new {id = redCard.Id});
                    return;
                }

            }
               
            var yellowCards = await GetLastYellowCardsValid(championship.Id, user.Id);
            if(yellowCards.Count != 0)
            {
                var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 1);
                if(yellowCards.Count == 3 && isNextMatch)
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch(yellowCards[0].MatchId, user.Id))
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
            } 
        }

        else
        {
            var redCard = await GetRedCardValid(championship.Id, user.Id);
            var yellowCards = await GetLastYellowCardsValid(championship.Id, user.Id);
            var isNextMatch = false;
            if(redCard is not null)
            {
                var previousMatch = await GetMatchById(redCard.MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, user, 1);
                if(isNextMatch)
                {
                    await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", new {id = redCard.Id});
                    return;
                }   
                
            }
              
            if(yellowCards.Count != 0)
            {
                var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 1);
                if(yellowCards.Count == 5 && isNextMatch)
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }

                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                if(isNextMatch && yellowCards.Count == 5 && await CheckIfReceiveRedCardInLastMatch(yellowCards[0].MatchId, user.Id))
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
                var redMatch = await GetRedMatchInvalid(user.Id, championship.Id);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(redMatch, match, user, 2);
                if(isNextMatch && yellowCards.Count == 3)
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
            } 
        }
    }
    private async Task InvalidingPlayerTempCards(PlayerTempProfile player, Championship championship, Match match)
    {
        if(match.Phase != 0)
        {
            var redCard = await GetRedCardValid2(championship.Id, player.Id);
            var isNextMatch = false;
            if(redCard is not null)
            {
                var previousMatch = await GetMatchById(redCard.MatchId);
                isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch, match, player);

                if(isNextMatch)
                {
                    await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", new {id = redCard.Id});
                    return;
                }
            }

            var yellowCards = await GetLastYellowCardsValid2(championship.Id, player.Id);
            if(yellowCards.Count != 0)
            {
                var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch2, match, player);
                if(yellowCards.Count == 3 && isNextMatch)
                {
                    foreach (var YellowCard in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", 
                            new {id = YellowCard.Id});
                    }
                    return;
                }
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, player, 2);
                if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch2(yellowCards[0].MatchId, player.Id))
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
            }
        }

        else if(championship.Format == Enum.Format.GroupStage)
        {
            var redCard = await GetRedCardValid2(championship.Id, player.Id);
            var isNextMatch = false;
            if(redCard is not null)
            {
                var previousMatch = await GetMatchById(redCard.MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, player, 1);

                if(isNextMatch)
                {
                    await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", new {id = redCard.Id});
                    return;
                }
            }
           
            var yellowCards = await GetLastYellowCardsValid2(championship.Id, player.Id);
            if(yellowCards.Count != 0)
            {
                var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, player, 1);
                if(yellowCards.Count == 3 && isNextMatch)
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, player, 2);
                if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch2(yellowCards[0].MatchId, player.Id))
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
            }
        }

        else
        {
            var redCard = await GetRedCardValid2(championship.Id, player.Id);
            var yellowCards = await GetLastYellowCardsValid2(championship.Id, player.Id);
            var isNextMatch = false;
            if(redCard is not null)
            {
                var previousMatch = await GetMatchById(redCard.MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, player, 1);
                if(isNextMatch)
                {
                    await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id", new {id = redCard.Id});
                    return;
                }
            }
          
            if(yellowCards.Count != 0)
            {
                var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, player, 1);
                if(yellowCards.Count == 5 && isNextMatch)
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }

                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, player, 2);
                if(isNextMatch && yellowCards.Count == 5 && await CheckIfReceiveRedCardInLastMatch2(yellowCards[0].MatchId, player.Id))
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
                var redMatch = await GetRedMatchInvalid2(player.Id, championship.Id);
                isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(redMatch, match, player, 2);
                if(isNextMatch && yellowCards.Count == 3)
                {
                    foreach (var card in yellowCards)
                    {
                        await _dbService.EditData("UPDATE Fouls SET Valid = false WHERE Id = @id ", new {id = card.Id});
                    }
                    return;
                }
            }
        }
    }
    private async Task<List<Foul>> GetLastYellowCardsValid(int championshipId, Guid userId)
        => await _dbService.GetAll<Foul>(
            @"SELECT f.* FROM Fouls f
            JOIN Matches m ON f.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND 
            f.PlayerId = @userId AND f.Considered = true AND 
            f.Valid = true AND f.YellowCard = true 
            ORDER BY f.Id DESC", 
            new {championshipId, userId});
    private async Task<List<Foul>> GetLastYellowCardsValid2(int championshipId, Guid userId)
        => await _dbService.GetAll<Foul>(
            @"SELECT f.* FROM Fouls f
            JOIN Matches m ON f.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND 
            f.PlayerTempId = @userId AND f.Considered = true AND 
            f.Valid = true AND f.YellowCard = true 
            ORDER BY f.Id DESC", 
            new {championshipId, userId});
    private async Task<Foul> GetRedCardValid(int championshipId, Guid userId)
        => await _dbService.GetAsync<Foul>(
            @"SELECT f.* FROM Fouls f
            JOIN Matches m ON f.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND 
            f.PlayerId = @userId AND 
            f.YellowCard = false AND f.Valid = true", 
        new {championshipId, userId});
    private async Task<Foul> GetRedCardValid2(int championshipId, Guid playerId)
        => await _dbService.GetAsync<Foul>(
            @"SELECT f.* FROM Fouls f
            JOIN Matches m ON f.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId 
            AND f.PlayerTempId = @playerId 
            AND f.YellowCard = false AND f.Valid = true", 
            new {championshipId, playerId});
    private bool CheckIfIsTheNextMatchToEliminations(Match previousMatch, Match nextMatch, User user)
    {
        var nextPhase = previousMatch?.Phase + 1;
        if(nextMatch.Phase == nextPhase && (user.PlayerTeamId == nextMatch.Home || user.PlayerTeamId == nextMatch.Visitor))
            return true;

        return false;
    }
    private bool CheckIfIsTheNextMatchToEliminations(Match previousMatch, Match nextMatch, PlayerTempProfile player)
    {
        var nextPhase = previousMatch?.Phase + 1;
        if(nextMatch.Phase == nextPhase && (player.TeamsId == nextMatch.Home ||player.TeamsId  == nextMatch.Visitor))
            return true;

        return false;
    }
    private bool CheckIfIsTheNextMatchToLeagueSystem(Match previousMatch, Match nextMatch, User user, int number)
    {
        var round = previousMatch?.Round + number;
        if(nextMatch.Round == round && (user.PlayerTeamId == nextMatch.Home || user.PlayerTeamId == nextMatch.Visitor))
            return true;

        return false;
    }
    private bool CheckIfIsTheNextMatchToLeagueSystem(Match previousMatch, Match nextMatch, PlayerTempProfile player, int number)
    {
        var round = previousMatch?.Round + number;
        if(nextMatch.Round == round && (player.TeamsId == nextMatch.Home || player.TeamsId == nextMatch.Visitor))
            return true;

        return false;
    }
    private async Task<bool> CheckIfFirstMatchHasNotFinished(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND (WINNER IS NULL AND Tied = false))", new {matchId});
    private async Task<int> DefineWinner(int teamId, int matchId)
    {
        if(teamId == 0)
        {
            await UpdateMatchToDefineTie(matchId);
            var match = await GetMatchById(matchId);
            var championship = await GetChampionshipByMatchId(matchId);
            if(await CheckIfMatchesOfCurrentPhaseHaveEnded(match.ChampionshipId, match.Phase) && match.Phase != Phase.Finals)
            {
                var newPhase = match.Phase + 1;

                if((newPhase != Phase.Finals && championship.DoubleMatchEliminations) || (newPhase == Phase.Finals && championship.FinalDoubleMatch))
                {
                    var winners = await GetWinners(championship, match.Phase); 
                    for (int i = 0; i <= winners.Count() / 2; i = i + 2)
                    {
                        var newMatch = new Match(match.ChampionshipId, winners[i], winners[i+1], newPhase);
                        var previousMatch = await CreateMatchSend(newMatch);
                        var newMatch2 = new Match(match.ChampionshipId, winners[i+1], winners[i], newPhase, previousMatch.Id);
                        await CreateMatchSend2(newMatch2);
                    }
                }

                else
                {
                    var winners = await GetWinners(championship, match.Phase);
                    for (int i = 0; i <= winners.Count() / 2; i = i + 2)
                    {
                        var newMatch = new Match(match.ChampionshipId, winners[i], winners[i+1], newPhase);
                        await CreateMatchSend(newMatch);
                    }
                }
                
            }
            return matchId;
        }
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
    private async Task<List<int>> GetWinners(Championship championship, Phase phase)
    {
        var winners = new List<int>();
        var aux = 1;
        var matches = await _dbService.GetAll<Match>("SELECT * FROM matches WHERE ChampionshipId = @championshipId AND Phase = @phase ORDER BY Id", new {championship.Id, phase});
        if((championship.DoubleMatchEliminations && phase != Phase.Finals) || (championship.FinalDoubleMatch && phase == Phase.Finals))
            aux = 2;

        for (int i = 0; i < matches.Count(); i = i + aux)
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
        return winners;
    }
    private async Task<bool> CheckIfIsLastMatch(int matchId)
        => await _dbService.GetAsync<bool>(
            @"SELECT EXISTS(
                SELECT * FROM matches 
                WHERE 
                (Id = (SELECT PreviousMatch FROM matches WHERE Id = @matchId) AND (Winner IS NOT NULL OR Tied = TRUE)) OR
                (PreviousMatch = @matchId AND (Winner IS NOT NULL OR Tied = TRUE))
            )", 
            new {matchId});
    private async Task<Match> CreateMatchSend2(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase, Round, PreviousMatch) VALUES(@ChampionshipId, @Home, @Visitor, @Phase, @Round, @PreviousMatch) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
    private async Task<Championship> GetChampionshipByMatchId(int matchId)
        => await _dbService.GetAsync<Championship>(
            @"SELECT c.*
            FROM Championships c
            JOIN Matches m ON c.Id = m.ChampionshipId
            WHERE m.Id = @matchId;
            ", 
            new {matchId});
    private async Task<Championship> GetByIdSend(int id) 
		=> await _dbService.GetAsync<Championship>("SELECT * FROM championships WHERE id = @id", new { id });
    private async Task<Match> CreateMatchSend(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase, Round) VALUES(@ChampionshipId, @Home, @Visitor, @Phase, @Round) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
    private async Task<bool> CheckIfMatchesOfCurrentPhaseHaveEnded(int championshipId, Phase phase)
    {
        var numberOfMatches = await _dbService.GetAsync<int>("SELECT Count(*) from matches WHERE ChampionshipId = @championshipId AND Phase = @phase", new {championshipId, phase});
        var numberOfMatchesClosed = await _dbService.GetAsync<int>("SELECT Count(*) from matches WHERE ChampionshipId = @championshipId AND Phase = @phase AND (Winner IS NOT NULL OR Tied = TRUE)", new {championshipId, phase});
        
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
    private async Task<int> GetPointsFromTeamByIdInTwoMatches(int matchId, int teamId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(*) FROM goals 
            WHERE (MatchId = @matchId OR MatchId = (SELECT Id FROM matches WHERE PreviousMatch = @matchId) OR MatchId =  (SELECT PreviousMatch FROM matches WHERE id = @matchId)) AND 
            (TeamId = @teamId AND OwnGoal = false OR TeamId <> @teamId AND OwnGoal = true)",
        new {matchId, teamId});
    private async Task<bool> DepartureDateNotSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND date IS NULL);", new {matchId});
    private async Task<bool> CheckIfThereIsWinner(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE id = @matchId AND Winner IS NOT NULL)", new {matchId});

    public async Task EndGameToLeagueSystemValidationAsync(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);
        if(match is null)
        {
            throw new ApplicationException("Partida passada não existe.");
        }
        if(await DepartureDateNotSet(matchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }
        if(match.Date.ToUniversalTime() >= DateTime.UtcNow)
        {
            throw new ApplicationException("Partida ainda não inciou");
        }
        if(match.Winner != 0)
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }
        if(match.Tied)
        {
            throw new ApplicationException("Partida já terminou em empate.");
        }
        if(match.HomeUniform is null || match.VisitorUniform is null)
            throw new ApplicationException("É necessário definir os uniformes das equipes antes");
        
        if(match.Road is null)
            throw new ApplicationException("É necessário definir o local da partida antes");
        if(await CheckIfLastMatchHasEnded(match))
            throw new ApplicationException("Partida anterior de um dos times anda não foi finalizada");

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

        var users = await GetAllUsersByTeamsId(match.Home, match.Visitor);
        var tempUsers = await GetAllTempProfileByTeamsId(match.Home, match.Visitor);

        foreach (var user in users)
        {
            await InvalidingUserCards(user, championship, match);
        }

        foreach (var temp in tempUsers)
        {
            await InvalidingPlayerTempCards(temp, championship, match);
        }

    }
    private async Task<bool> CheckIfLastMatchHasEnded(Match match)
        => await _dbService.GetAsync<bool>(
            @"SELECT EXISTS(
                SELECT * FROM Matches 
                WHERE ChampionshipId = @championshipId AND 
                Round = @round AND 
                ((Visitor = @team1 OR Home = @team1) OR (Visitor = @team2 OR Home = @team2)) AND
                Winner IS NULL AND Tied = false
            )",
            new {championshipId = match.ChampionshipId, round = match.Round-1, team1 = match.Home, team2 = match.Visitor});
    private async Task<bool> CheckIfThereIsTie(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM matches WHERE Id = @matchId AND Tied = true)", new {matchId});
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
        var result = goalsScored - goalsConceded;
        return result;
    }
    private async Task<int> ProGoals(int teamId, int championshipId)
        => await _dbService.GetAsync<int>(
            @"SELECT COUNT(g.Id)
            FROM Goals g
            JOIN Matches m ON g.MatchId = m.Id
            WHERE m.ChampionshipId = @championshipId AND (m.Visitor = @teamId OR m.Home = @teamId) AND
            ((g.TeamId = @teamId AND g.OwnGoal = false) OR (g.TeamId <> @teamId AND g.OwnGoal = true))",
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
        var team = await GetByTeamIdSendAsync(homeClassification.TeamId);
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

                                else if(arrayTeamQualifyingGoal == homeTeamQualifyingGoal && team.SportsId == 1)
                                {
                                    var arrayTeamYellowCards = await AmountOfYellowCards(classifications[i].TeamId, classifications[i].ChampionshipId);
                                    var homeTeamYellowCards = await AmountOfYellowCards(homeClassification.TeamId, homeClassification.ChampionshipId);

                                    if(arrayTeamYellowCards > homeTeamYellowCards)
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
                                    else if(arrayTeamYellowCards == homeTeamYellowCards)
                                    {
                                        var arrayTeamRedCards = await AmountOfRedCards(classifications[i].TeamId, classifications[i].ChampionshipId);
                                        var homeTeamRedCards = await AmountOfRedCards(homeClassification.TeamId, homeClassification.ChampionshipId);

                                        if(arrayTeamRedCards > homeTeamRedCards)
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

                                        else if(arrayTeamRedCards == homeTeamRedCards)
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

                                else
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
    private async Task<int> AmountOfRedCards(int teamId, int championshipId)
    {
        var tempCards = await _dbService.GetAsync<int>(
            @"SELECT COUNT(*)
            FROM Fouls f
            JOIN PlayerTempProfiles p ON f.PlayerTempId = p.Id
            JOIN Matches m ON m.Id = f.MatchId
            WHERE p.TeamsId = @teamId AND m.ChampionshipId = @championshipId AND f.YellowCard = false;", 
            new {teamId, championshipId});
        var userCards = await _dbService.GetAsync<int>(
            @"SELECT COUNT(*)
            FROM Fouls f
            JOIN Users u ON f.PlayerId = u.Id
            JOIN Matches m ON m.Id = f.MatchId
            WHERE u.PlayerTeamId = @teamId AND m.ChampionshipId = @championshipId AND f.YellowCard = false;", 
            new {teamId, championshipId});
        return tempCards + userCards;
    }
    private async Task<int> AmountOfYellowCards(int teamId, int championshipId)
    {
        var tempCards = await _dbService.GetAsync<int>(
            @"SELECT COUNT(*)
            FROM Fouls f
            JOIN PlayerTempProfiles p ON f.PlayerTempId = p.Id
            JOIN Matches m ON m.Id = f.MatchId
            WHERE p.TeamsId = @teamId AND m.ChampionshipId = @championshipId AND f.YellowCard = true AND f.Considered = true;", 
            new {teamId, championshipId});
        var userCards = await _dbService.GetAsync<int>(
            @"SELECT COUNT(*)
            FROM Fouls f
            JOIN Users u ON f.PlayerId = u.Id
            JOIN Matches m ON m.Id = f.MatchId
            WHERE u.PlayerTeamId = @teamId AND m.ChampionshipId = @championshipId AND f.YellowCard = true AND f.Considered = true;", 
            new {teamId, championshipId});
        return tempCards + userCards;
    }

    public async Task EndGameToGroupStageValidationAsync(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);
        if(match is null)
        {
            throw new ApplicationException("Partida passada não existe.");
        }
        if(await DepartureDateNotSet(matchId))
        {
            throw new ApplicationException("Data da partida não definida.");
        }
        if(match.Date.ToUniversalTime() >= DateTime.UtcNow)
        {
            throw new ApplicationException("Partida ainda não inciou");
        }
        if(match.Winner != 0)
        {
            throw new ApplicationException("Partida já possui um vencedor.");
        }
        if(match.Tied)
        {
            throw new ApplicationException("Partida já terminou em empate.");
        }

        if(match.HomeUniform is null || match.VisitorUniform is null)
            throw new ApplicationException("É necessário definir os uniformes das equipes antes");
        
        if (match.Road is null)
            throw new ApplicationException("É necessário definir o local da partida antes antes");
        
        if(await CheckIfLastMatchHasEnded(match))
            throw new ApplicationException("Partida anterior de um dos times anda não foi finalizada");

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
            await _bracketingService.CreateKnockoutToGroupStageValidationAsync(teamsId, match.ChampionshipId);
        }

        var users = await GetAllUsersByTeamsId(match.Home, match.Visitor);
        var tempUsers = await GetAllTempProfileByTeamsId(match.Home, match.Visitor);

        foreach (var user in users)
        {
            await InvalidingUserCards(user, championship, match);
        }

        foreach (var temp in tempUsers)
        {
            await InvalidingPlayerTempCards(temp, championship, match);
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

    public async Task<List<string>> UpdateMatchValidationAsync(Match match)
    {
        var result = await new MatchValidator().ValidateAsync(match);
		
		if (!result.IsValid)
			return result.Errors.Select(x => x.ErrorMessage).ToList();

        var oldMatch = await GetMatchById(match.Id);

        if(oldMatch is null)
            throw new ApplicationException("Partida não existe.");

        match.Date = match.Date.ToUniversalTime();
        var championship = await GetByIdSend(oldMatch.ChampionshipId);

        if(championship.InitialDate.ToUniversalTime() > match.Date.Date || championship.FinalDate.ToUniversalTime() < match.Date.Date)
            throw new ApplicationException("Partida não pode ser anterior ou posterior ao seu campeonato");

        if(oldMatch.Tied == true || oldMatch.Winner != 0)
            throw new ApplicationException("Dados da partida não podem ser alterados após o seu encerramento");
        
        if(match.HomeUniform is not null)
        {
            if(!await CheckIfUniformBelongsToTeam(oldMatch.Home, match.HomeUniform))
                throw new ApplicationException("Time não apresenta o uniforme passado");
        }

        if(match.VisitorUniform is not null)
        {
            if(!await CheckIfUniformBelongsToTeam(oldMatch.Visitor, match.VisitorUniform))
                throw new ApplicationException("Time não apresenta o uniforme passado");
        }
        

        await UpdateSend(match);
        return new();
    }
    private async Task<bool> CheckIfUniformBelongsToTeam(int teamId, string uniform)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM teams WHERE Id = @teamId AND (UniformHome = @uniform OR UniformAway = @uniform))", new {teamId, uniform});
    private async Task UpdateSend(Match match)
		=> await _dbService.EditData(
            @"UPDATE Matches SET date = @Date, arbitrator = @Arbitrator, homeuniform = @HomeUniform, 
            visitoruniform = @VisitorUniform, Cep = @Cep, City = @City, Road = @Road, Number = @Number WHERE id=@id",
            match);
    public async Task ActiveProrrogationValidationAsync(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);

        if(match is null)
            throw new ApplicationException("Partida passada não existe");

        if(championship.SportsId == Sports.Volleyball)
            throw new ApplicationException("Vôlei não apresenta prorrogação");
        
        if(championship.Format == Format.LeagueSystem)
            throw new ApplicationException("Esse formato de campeonato não apresenta prorrogação");
        
        if(championship.Format == Format.GroupStage && match.Round != 0)
            throw new ApplicationException("Fase atual do campeonato não apresenta prorrogação");

        if(match.Phase != Phase.Finals && championship.DoubleMatchEliminations ||
            match.Phase == Phase.Finals && championship.FinalDoubleMatch)
        {
            var aggregateVisitorPoints = await GetPointsFromTeamByIdInTwoMatches(match.Id, match.Visitor);
            var aggregateHomePoints = await GetPointsFromTeamByIdInTwoMatches(match.Id, match.Home);
            if(match.PreviousMatch == 0)
            {
                throw new ApplicationException("Primeira partida não pode apresentar prorrogação");
            }
            else if(await CheckIfFirstMatchHasNotFinished(match.PreviousMatch))
            {
               throw new ApplicationException("Primeira partida ainda não foi finalizada");
            }
            else if(aggregateHomePoints != aggregateVisitorPoints)
            {
                throw new ApplicationException("Partida precisa estar empatada para iniciar a prorrogação");
            }
        }

        else
        {
            var visitorPoints = await GetPointsFromTeamById(matchId, match.Visitor);
            var homePoints = await GetPointsFromTeamById(matchId, match.Home);

            if(visitorPoints != homePoints)
                throw new ApplicationException("Partida precisa estar empatada para iniciar a prorrogação");
        }
        
        await _dbService.EditData("UPDATE Matches SET Prorrogation = true WHERE id=@matchId", new {matchId});
    }
    public async Task<MatchDTO> GetMatchByIdValidation(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);

        if(match is null)
            throw new ApplicationException("Partida passada não existe");
        
        if(championship.SportsId == Sports.Football)
		{
            var matchDTO = new MatchDTO();
            var home = await GetByTeamIdSendAsync(match.Home);
            var visitor = await GetByTeamIdSendAsync(match.Visitor);
            matchDTO.Id = match.Id;
            matchDTO.IsSoccer = true;
            matchDTO.Prorrogation = match.Prorrogation;
            matchDTO.HomeEmblem = home.Emblem;
            matchDTO.HomeName = home.Name;
            matchDTO.HomeId = home.Id;
            matchDTO.HomeUniformHome = home.UniformHome;
            matchDTO.HomeUniformAway = home.UniformAway;
            matchDTO.HomeGoals = await GetPointsFromTeamById(match.Id, match.Home);
            matchDTO.VisitorEmblem = visitor.Emblem;
            matchDTO.VisitorName = visitor.Name;
            matchDTO.VisitorUniformHome = visitor.UniformHome;
            matchDTO.VisitorUniformAway = visitor.UniformAway;
            matchDTO.VisitorGoals = await GetPointsFromTeamById(match.Id, match.Visitor);
            matchDTO.VisitorId = visitor.Id;
            matchDTO.Cep = match.Cep;
            matchDTO.City = match.City;
            matchDTO.Road = match.Road;
            matchDTO.Number = match.Number;
            matchDTO.MatchReport = match.MatchReport;
            matchDTO.Arbitrator = match.Arbitrator;
            matchDTO.Date = match.Date;
            matchDTO.ChampionshipId = match.ChampionshipId;
            matchDTO.Finished = (match.Winner != 0 || match.Tied == true) ? true : false;
			return matchDTO;
		}

		else
		{
            var matchDTO = new MatchDTO();
            var homeTeam = await GetByTeamIdSendAsync(match.Home);
            var visitorTeam = await GetByTeamIdSendAsync(match.Visitor);
            matchDTO.Id = match.Id;
            matchDTO.HomeEmblem = homeTeam.Emblem;
            matchDTO.HomeName = homeTeam.Name;
            matchDTO.Prorrogation = match.Prorrogation;
            matchDTO.HomeId = homeTeam.Id;
            matchDTO.VisitorId = visitorTeam.Id;
            matchDTO.VisitorEmblem = visitorTeam.Emblem;
            matchDTO.VisitorName = visitorTeam.Name;
            matchDTO.Cep = match.Cep;
            matchDTO.City = match.City;
            matchDTO.Road = match.Road;
            matchDTO.Number = match.Number;
            matchDTO.MatchReport = match.MatchReport;
            matchDTO.Arbitrator = match.Arbitrator;
            matchDTO.Date = match.Date;
            matchDTO.ChampionshipId = match.ChampionshipId;
            matchDTO.Finished = (match.Winner != 0 || match.Tied == true) ? true : false;
            var pointsForSet = new List<int>();
            var pointsForSet2 = new List<int>();
            var WonSets = 0;
            var WonSets2 = 0;
            var lastSet = 0;
            lastSet = !await IsItFirstSet(match.Id) ? 1 : await GetLastSet(match.Id);
            var team2Id = await _dbService.GetAsync<int>("SELECT CASE WHEN home <> @teamId THEN home ELSE visitor END AS selected_team FROM matches WHERE id = @matchId;", new {teamId = match.Home, matchId = match.Id});

            for (int i = 0;  i < lastSet; i++)
            {
                pointsForSet.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId = @teamId And OwnGoal = false OR TeamId <> @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId = match.Home, j = i+1}));
                pointsForSet2.Add(await _dbService.GetAsync<int>("select count(*) from goals where MatchId = @matchId AND (TeamId <> @teamId And OwnGoal = false OR TeamId = @teamId And OwnGoal = true) AND Set = @j", new {matchId = match.Id, teamId = match.Home, j = i+1}));
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
            matchDTO.HomeWinnigSets = WonSets;
            matchDTO.VisitorWinnigSets = WonSets2;
            return matchDTO;
		}
    }
	private async Task<Team> GetByTeamIdSendAsync(int id) => await _dbService.GetAsync<Team>("SELECT * FROM teams where id=@id AND deleted = false", new {id});
    private async Task<bool> IsItFirstSet(int matchId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM goals WHERE MatchId = @matchId);", new {matchId});
    private async Task<int> GetLastSet(int matchId)
    	=> await _dbService.GetAsync<int>("SELECT MAX(Set) from goals where MatchId = @matchId", new {matchId});
    
    public async Task<bool> CanThereBePenalties(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);

        if(match is null)
            throw new ApplicationException("Partida passada não existe");
        
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
            {
                return false;
            }
            else if(await CheckIfFirstMatchHasNotFinished(match.PreviousMatch))
            {
                return false;
            }
            else if(aggregateHomePoints != aggregateVisitorPoints)
            {
                return false;
            }
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

    public async Task<List<User>> GetAllPlayersValidInTeamValidation(int matchId, int teamId)
    {
        var match = await GetMatchById(matchId);

        if(match is null)
            throw new ApplicationException("Partida passada não existe");
        
        var players = await GetPlayersOfteamSend(teamId);
        var length = players.Count;

        for (int i = 0; i < length; i++)
        {
            if(await CheckIfIsSuspended(players[i], match))
            {
				players.Remove(players[i]);
                length = length - 1;
            }
        }
		return players;
    }

    private async Task<bool> CheckIfIsSuspended(User user, Match match)
	{
        var championship = await GetChampionshipByMatchId(match.Id);
		if(!string.IsNullOrWhiteSpace(user.Username))
		{
            if(match.Phase != 0)
            {
                var redCard = await GetRedCardValid(championship.Id, user.Id);
                var isNextMatch = false;
                if(redCard is not null)
                {
                    var previousMatch = await GetMatchById(redCard.MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch, match, user);

                    if(redCard.MatchId == match.Id)
                        return true;
                    
                    if(isNextMatch)
                        return true;
                }

                var yellowCards = await GetLastYellowCardsValid(championship.Id, user.Id);
                if(yellowCards.Count != 0)
                {
                    var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch2, match, user);
                    if(yellowCards.Count == 3 && isNextMatch)
                        return true;
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch(yellowCards[0].MatchId, user.Id))
                        return true;
                }

                return false;
            }

            else if(championship.Format == Enum.Format.GroupStage)
            {
                var redCard = await GetRedCardValid(championship.Id, user.Id);
                var isNextMatch = false;
                if(redCard is not null)
                {
                    var previousMatch = await GetMatchById(redCard.MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, user, 1);
                    if(redCard.MatchId == match.Id)
                        return true;

                    if(isNextMatch)
                        return true;
                }
               
                var yellowCards = await GetLastYellowCardsValid(championship.Id, user.Id);
                if(yellowCards.Count != 0)
                {
                    var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 1);
                    if(yellowCards.Count == 3 && isNextMatch)
                        return true;
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch(yellowCards[0].MatchId, user.Id))
                        return true;
                }

                return false;
            }

            else
            {
                var redCard = await GetRedCardValid(championship.Id, user.Id);
                var yellowCards = await GetLastYellowCardsValid(championship.Id, user.Id);
                var isNextMatch = false;
                if(redCard is not null)
                {
                    var previousMatch = await GetMatchById(redCard.MatchId);
                    
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, user, 1);
                    if(isNextMatch)
                        return true;
                    
                    if(redCard.MatchId == match.Id)
                        return true;
                }

                if(yellowCards.Count != 0)
                {
                    var previousMatch2 = await GetMatchById(yellowCards[0].MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 1);
                    if(yellowCards.Count == 5 && isNextMatch)
                        return true;
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 5 && await CheckIfReceiveRedCardInLastMatch(yellowCards[0].MatchId, user.Id))
                        return true;
                    var redMatch = await GetRedMatchInvalid(user.Id, championship.Id);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(redMatch, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 3)
                        return true;
                }

                return false;
            }
		}

        else
        {
            if(match.Phase != 0)
            {
                var redCard = await GetRedCardValid2(championship.Id, user.Id);
                var isNextMatch = false;
                if(redCard is not null)
                {
                    var previousMatch = await GetMatchById(redCard.MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch, match, user);

                    if(redCard.MatchId == match.Id)
                        return true;

                    if(isNextMatch)
                        return true;
                }

                var yellowCards = await GetLastYellowCardsValid2(championship.Id, user.Id);
                if(yellowCards.Count != 0)
                {
                    var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToEliminations(previousMatch2, match, user);
                    if(yellowCards.Count == 3 && isNextMatch)
                        return true;
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch2(yellowCards[0].MatchId, user.Id))
                        return true;
                }

                return false;     
            }

            else if(championship.Format == Enum.Format.GroupStage)
            {
                var redCard = await GetRedCardValid2(championship.Id, user.Id);
                var isNextMatch = false;
                if(redCard is not null)
                {
                    var previousMatch = await GetMatchById(redCard.MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, user, 1);

                    if(redCard.MatchId == match.Id)
                        return true;

                    if(isNextMatch)
                       return true;
                }

                var yellowCards = await GetLastYellowCardsValid2(championship.Id, user.Id);
                if(yellowCards.Count != 0)
                {
                    var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 1);
                    if(yellowCards.Count == 3 && isNextMatch)
                        return true;
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 3 && await CheckIfReceiveRedCardInLastMatch2(yellowCards[0].MatchId, user.Id))
                        return true;
                }

                return false;  
            }

            else
            {
                var redCard = await GetRedCardValid2(championship.Id, user.Id);
                var yellowCards = await GetLastYellowCardsValid2(championship.Id, user.Id);
                var isNextMatch = false;
                if(redCard is not null)
                {
                    var previousMatch = await GetMatchById(redCard.MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch, match, user, 1);
                    if(isNextMatch)
                        return true;
                    
                    if(redCard.MatchId == match.Id)
                        return true;
                }
                
                if(yellowCards.Count != 0)
                {
                    var previousMatch2 =  await GetMatchById(yellowCards[0].MatchId);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 1);
                    if(yellowCards.Count == 5 && isNextMatch)
                        return true;
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(previousMatch2, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 5 && await CheckIfReceiveRedCardInLastMatch2(yellowCards[0].MatchId, user.Id))
                        return true;
                    
                    var redMatch = await GetRedMatchInvalid2(user.Id, championship.Id);
                    isNextMatch = CheckIfIsTheNextMatchToLeagueSystem(redMatch, match, user, 2);
                    if(isNextMatch && yellowCards.Count == 3)
                        return true;
                }

                return false;
            }
        }
	}
    private async Task<Match> GetRedMatchInvalid2(Guid playerId, int championshipId)
        => await _dbService.GetAsync<Match>(
        @"SELECT m.* FROM Matches m
        JOIN Fouls f ON f.MatchId = m.Id
        WHERE f.PlayerTempId = @playerId AND m.ChampionshipId = @championshipId AND f.YellowCard = false AND f.Valid = false", 
        new {playerId, championshipId});
    private async Task<Match> GetRedMatchInvalid(Guid userId, int championshipId)
        => await _dbService.GetAsync<Match>(
        @"SELECT m.* FROM Matches m
        JOIN Fouls f ON f.MatchId = m.Id
        WHERE f.PlayerId = @userId AND m.ChampionshipId = @championshipId AND f.YellowCard = false AND f.Valid = false", 
        new {userId, championshipId});
    private async Task<bool> CheckIfReceiveRedCardInLastMatch(int matchId, Guid userId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Fouls WHERE MatchId = @matchId AND YellowCard = false AND Valid = false AND PlayerId = @userId)", new {matchId, userId});
    private async Task<bool> CheckIfReceiveRedCardInLastMatch2(int matchId, Guid tempId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Fouls WHERE MatchId = @matchId AND YellowCard = false AND Valid = false AND PlayerTempId = @tempId)", new {matchId, tempId});
    private async Task<List<User>> GetPlayersOfteamSend(int id) =>
		await _dbService.GetAll<User>(
			@"
			SELECT id, name, artisticname, number, email, teamsid as playerteamid, playerposition, false as iscaptain, picture, null as username FROM playertempprofiles WHERE teamsid = @id
			UNION ALL
			SELECT id, name, artisticname, number, email, playerteamid, playerposition, iscaptain, picture, username FROM users WHERE playerteamid = @id;",
			new { id });
    
    public async Task AddMatchReportValidation(Match match)
    {
        var oldMatch = await GetMatchById(match.Id);

        if(oldMatch is null)
            throw new ApplicationException("Partida passada não existe");
        if(match.MatchReport is null)
            throw new ApplicationException("Súmula não pode ser nula");
        if(oldMatch.Winner == 0 && !oldMatch.Tied)
            throw new ApplicationException("Partida ainda não foi finalizada");
        
        await AddMatchReportSend(match);

    }
    private async Task AddMatchReportSend(Match match)
    {
        await _dbService.EditData("UPDATE Matches SET MatchReport = @MatchReport WHERE id = @Id", match);
    }

    public async Task<dynamic> GetAllEventsValidation(int matchId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);

        if(match is null)
            throw new ApplicationException("Partida passada não existe");
        
        if(championship.SportsId == Sports.Football)
        {
            var goals = await GetAllGoalsByMatchId(matchId);
            var fouls = await GetAllFoulsByMatcId(matchId);
            var penalties = await GetAllPenaltiesByMatchId(matchId);

            var events = new List<dynamic>();

            foreach (var goal in goals)
            {
                var player = await GetTempById(goal.PlayerTempId);
                var user =  await GetUserById(goal.PlayerId);
                var assisterUser =  await GetUserById(goal.AssisterPlayerId);
                var assisterTemp = await GetTempById(goal.AssisterPlayerTempId);
                var goalEvent = 
                new 
                {
                    Name = (player is null) ? user?.Name : player?.Name,
                    AssisterName = (assisterUser is null) ? assisterTemp?.Name : assisterTemp?.Name,
                    PlayerId = (goal.PlayerId == Guid.Empty) ? goal.PlayerTempId : goal.PlayerId,
                    AssisterPlayerId = (goal.AssisterPlayerTempId == Guid.Empty) ? goal.AssisterPlayerId : goal.AssisterPlayerTempId,
                    Minutes = goal.Minutes,
                    OwnGoal = goal.OwnGoal,
                    TeamId = goal.TeamId,
                    Goal = true,
                    Foul = false,
                    Penalty = false
                };
                events.Add(goalEvent);
            }

            foreach (var foul in fouls)
            {
                var name = "";
                var teamId = 0;
                if(foul.PlayerId == Guid.Empty)
                {
                    var temp = await GetTempById(foul.PlayerTempId);
                    name = temp.Name;
                    teamId = temp.TeamsId;
                }

                else
                {
                    var user = await GetUserById(foul.PlayerId);
                    name = user.Name;
                    teamId = user.PlayerTeamId;
                }
                var foulEvent = 
                new 
                {
                    Name = name,
                    PlayerId = (foul.PlayerId == Guid.Empty) ? foul.PlayerTempId : foul.PlayerId,
                    Minutes = foul.Minutes,
                    YellowCard = foul.YellowCard,
                    TeamId = teamId,
                    Goal = false,
                    Foul = true,
                    Penalty = false
                };
                events.Add(foulEvent);
            }

            events = events.OrderByDescending(e => e.Minutes).ToList();

            foreach (var penalty in penalties)
            {
                var player = await GetTempById(penalty.PlayerTempId);
                var user =  await GetUserById(penalty.PlayerId);
                var penaltyEvent = 
                new 
                {
                    Name = (player is null) ? user?.Name : player?.Name,
                    PlayerId = (penalty.PlayerId == Guid.Empty) ? penalty.PlayerTempId : penalty.PlayerId,
                    Converted = penalty.Converted,
                    TeamId = penalty.TeamId,
                    Goal = false,
                    Foul = false,
                    Penalty = true,
                };
                events.Insert(0, penaltyEvent);
            }

            return events;
        }

        else
        {
            var goals = await GetAllGoalsByMatchId(matchId);

            var events = new List<dynamic>();

            foreach (var goal in goals)
            {
                var player = await GetTempById(goal.PlayerTempId);
                var user =  await GetUserById(goal.PlayerId);
                var goalEvent = 
                new 
                {
                    Name = (player is null) ? user?.Name : player?.Name,
                    PlayerId = (goal.PlayerId == Guid.Empty) ? goal.PlayerTempId : goal.PlayerId,
                    Date = goal.Date,
                    OwnGoal = goal.OwnGoal,
                    Set = goal.Set,
                    TeamId = goal.TeamId,
                    Goal = true
                };
                events.Add(goalEvent);
            }

            events = events.OrderByDescending(e => e.Date).ToList();

            return events;
        }

    }
    private async Task<List<Goal>> GetAllGoalsByMatchId(int matchId)
        => await _dbService.GetAll<Goal>("SELECT * FROM Goals WHERE MatchId = @matchId", new {matchId});
    private async Task<List<Foul>> GetAllFoulsByMatcId(int matchId)
        => await _dbService.GetAll<Foul>("SELECT * FROM Fouls WHERE MatchId = @matchId", new {matchId});
    private async Task<List<Penalty>> GetAllPenaltiesByMatchId(int matchId)
        => await _dbService.GetAll<Penalty>("SELECT * FROM Penalties WHERE MatchId = @matchId ORDER BY Id", new {matchId});
    private async Task<PlayerTempProfile> GetTempById(Guid id)
        => await _dbService.GetAsync<PlayerTempProfile>("SELECT * FROM PlayerTempProfiles WHERE Id = @id", new {id});
    private async Task<User> GetUserById(Guid id)
        => await _dbService.GetAsync<User>("SELECT * FROM Users WHERE Id = @id", new {id});

    public async Task WoValidation(int matchId, int teamId)
    {
        var match = await GetMatchById(matchId);
        var championship = await GetChampionshipByMatchId(matchId);

        var player = await GetPlayerOfteamSend(teamId != match.Visitor ? match.Visitor : match.Home );

        if(match is null)
            throw new ApplicationException("Partida passada não existe");
        if(await DepartureDateNotSet(matchId))
            throw new ApplicationException("Data da partida não definida.");
        if(match.Date.ToUniversalTime() >= DateTime.UtcNow)
            throw new ApplicationException("Partida ainda não inciou");
        if(match.Winner != 0)
            throw new ApplicationException("Partida já possui um vencedor.");
        if(match.HomeUniform is null || match.VisitorUniform is null)
            throw new ApplicationException("É necessário definir os uniformes das equipes antes");
        if (match.Road is null)
            throw new ApplicationException("É necessário definir o local da partida antes antes");

        if(match.Round != 0 && championship.Format == Format.GroupStage)
        {
            if(match.Tied)
                throw new ApplicationException("Partida já terminou em empate.");
            if(await CheckIfLastMatchHasEnded(match))
                throw new ApplicationException("Partida anterior de um dos times anda não foi finalizada");
            
            await _goalService.RemoveAllGoalOfMatchValidation(match.Id);

            if(string.IsNullOrWhiteSpace(player.Username))
            {
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerTempId = player.Id;
                    goal.Minutes = 0;
                    goal.OwnGoal = true;
                    goal.TeamId = teamId != match.Visitor ? match.Visitor : match.Home;
                    await CreateGoalToPlayerTempSend(goal);
                }
            }

            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerId = player.Id;
                    goal.TeamId = teamId != match.Visitor ? match.Visitor : match.Home;
                    goal.Minutes = 0;
                    goal.OwnGoal = true;
                    await CreateGoalToPlayerSend(goal);
                }
            }
            
            await EndGameToGroupStageValidationAsync(match.Id);
        }
        else if(match.Round != 0 && championship.Format == Format.LeagueSystem)
        {
            if(match.Tied)
                throw new ApplicationException("Partida já terminou em empate.");
            if(await CheckIfLastMatchHasEnded(match))
                throw new ApplicationException("Partida anterior de um dos times anda não foi finalizada");
            
            await _goalService.RemoveAllGoalOfMatchValidation(match.Id);

            if(string.IsNullOrWhiteSpace(player.Username))
            {
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerTempId = player.Id;
                    goal.Minutes = 0;
                    goal.TeamId = teamId != match.Visitor ? match.Visitor : match.Home;
                    goal.OwnGoal = true;
                    await CreateGoalToPlayerTempSend(goal);
                }
            }

            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerId = player.Id;
                    goal.Minutes = 0;
                    goal.TeamId = teamId != match.Visitor ? match.Visitor : match.Home;
                    goal.OwnGoal = true;
                    await CreateGoalToPlayerSend(goal);
                }
            }

            await EndGameToLeagueSystemValidationAsync(match.Id);
        }

        else
        {
            await _goalService.RemoveAllGoalOfMatchValidation(match.Id);

            if(string.IsNullOrWhiteSpace(player.Username))
            {
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerTempId = player.Id;
                    goal.TeamId = teamId != match.Visitor ? match.Visitor : match.Home;
                    goal.Minutes = 0;
                    goal.OwnGoal = true;
                    await CreateGoalToPlayerTempSend(goal);
                }
            }

            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var goal = new Goal();
                    goal.MatchId = match.Id;
                    goal.PlayerId = player.Id;
                    goal.TeamId = teamId != match.Visitor ? match.Visitor : match.Home;
                    goal.Minutes = 0;
                    goal.OwnGoal = true;
                    await CreateGoalToPlayerSend(goal);
                }
            }
            await EndGameToKnockoutValidationAsync(matchId);
        }
    }
    private async Task<User> GetPlayerOfteamSend(int id) =>
		await _dbService.GetAsync<User>(
			@"
			SELECT id, name, artisticname, number, email, teamsid as playerteamid, playerposition, false as iscaptain, picture, null as username FROM playertempprofiles WHERE teamsid = @id
			UNION ALL
			SELECT id, name, artisticname, number, email, playerteamid, playerposition, iscaptain, picture, username FROM users WHERE playerteamid = @id;",
			new { id });
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
      
}