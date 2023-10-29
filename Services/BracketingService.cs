using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class BracketingService
{
    private readonly DbService _dbService;

    public BracketingService(DbService dbService)
	{
		_dbService = dbService;
	}

	public async Task<List<Match>> CreateKnockoutValidationAsync(int championshipId)
	{
		if(!await CheckIfChampionhipExists(championshipId))
        {
            throw new ApplicationException("Campeonato passado não existe.");
        }
		var championship = await GetByIdSend(championshipId);
		var teams = await GetAllTeamsOfChampionshipSend(championshipId);

		if(championship.TeamQuantity != teams.Count())
		{
            throw new ApplicationException("Campeonato passado com quantidade inválida de times.");
        }

		if(championship.Format != Format.Knockout)
		{
			throw new ApplicationException("Campeonato passado não apresenta o formato de eliminatórias com partida única.");
		}

		var matches = new List<Match>();
		var number = 64;
		var phase = Phase.ThirtySecondOfFinal;
		var teamQuantityInitial = teams.Count()/2;
		for (int i = 1; i < 7; i++)
		{
			if(!(teams.Count() == number))
			{
				phase++;
				number = number / 2;
			} 
		}

		if((phase != Phase.Finals && championship.DoubleMatchEliminations) || (phase == Phase.Finals && championship.FinalDoubleMatch))
		{
			for (int i = 0, j= 0; i < teamQuantityInitial; i++, j = j+2)
			{
				var numberRandom = new Random().Next(0, teams.Count());
				var numberRandom2 = new Random().Next(0, teams.Count());
				while (numberRandom2 == numberRandom)
				{
					numberRandom2 = new Random().Next(0, teams.Count());
				}

				var match = new Match(championshipId, teams[numberRandom].Id, teams[numberRandom2].Id, phase);
				
				teams.RemoveAt(numberRandom);
				if(numberRandom2 > numberRandom)
				{
					teams.RemoveAt(numberRandom2 - 1);
				} else {
					teams.RemoveAt(numberRandom2);
				}			
				matches.Add(await CreateMatchSend(match));
				var match2 = new Match(championshipId, matches[j].Visitor, matches[j].Home, phase, matches[j].Id);
				matches.Add(await CreateMatchSend2(match2));
			}
		}

		else
		{
			for (int i = 0; i < teamQuantityInitial; i++)
			{
				var numberRandom = new Random().Next(0, teams.Count());
				var numberRandom2 = new Random().Next(0, teams.Count());
				while (numberRandom2 == numberRandom)
				{
					numberRandom2 = new Random().Next(0, teams.Count());
				}

				var match = new Match(championshipId, teams[numberRandom].Id, teams[numberRandom2].Id, phase);
				teams.RemoveAt(numberRandom);
				if(numberRandom2 > numberRandom)
				{
					teams.RemoveAt(numberRandom2 - 1);
				} else {
					teams.RemoveAt(numberRandom2);
				}			
				matches.Add(await CreateMatchSend(match));
			}
		}
		return matches;
	}
	private async Task<Match> CreateMatchSend(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase, Round) VALUES(@ChampionshipId, @Home, @Visitor, @Phase, @Round) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
	private async Task<Match> CreateMatchSend2(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase, Round, PreviousMatch) VALUES(@ChampionshipId, @Home, @Visitor, @Phase, @Round, @PreviousMatch) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
	private async Task<List<Team>> GetAllTeamsOfChampionshipSend(int championshipId)
		=> await _dbService.GetAll<Team>("SELECT c.emblem, c.name, c.id FROM teams c JOIN championships_teams ct ON c.id = ct.teamId AND ct.championshipid = @championshipId WHERE ct.Accepted = true;", new { championshipId });
	private async Task<bool> CheckIfChampionhipExists(int championshipId)
        => await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM championships WHERE id = @championshipId)", new {championshipId});

	public async Task<List<Match>> CreateLeagueSystemValidationAsync(int championshipId)
	{
		if(!await CheckIfChampionhipExists(championshipId))
        {
            throw new ApplicationException("Campeonato passado não existe.");
        }
		var championship = await GetByIdSend(championshipId);
		var teams = await GetAllTeamsOfChampionshipSend(championshipId);

		if(championship.TeamQuantity != teams.Count())
		{
            throw new ApplicationException("Campeonato passado com quantidade inválida de times.");
        }

		if(championship.Format != Format.LeagueSystem)
		{
			throw new ApplicationException("Campeonato passado não apresenta o formato de pontos corridos.");
		}

		teams.Sort((x, y) => string.Compare(x.Name, y.Name));

		for (int i = 0; i < teams.Count(); i++)
		{
			await CreateClassificationSend(new Classification(0, teams[i].Id, championshipId, i+1));
		}

		var matches = new List<Match>();

		Shuffle(teams);

		int numberOfCompetitors = teams.Count;
        int numberOfRounds = numberOfCompetitors - 1;

        for (int round = 1; round <= numberOfRounds; round++)
        {
            for (int i = 0; i < numberOfCompetitors / 2; i++)
            {
                Team homeTeam = teams[i];
                Team awayTeam = teams[numberOfCompetitors - 1 - i];
				matches.Add(new Match(championshipId, homeTeam.Id, awayTeam.Id, round));
            }

            await RotateTeams(teams);
        }
		
		matches.Sort((x, y) => x.Round.CompareTo(y.Round));

		if(championship.DoubleStartLeagueSystem)
		{
			var quantityMatches = matches.Count();
			for (int i = 0; i < quantityMatches; i++)
			{
				matches.Add(new Match(championshipId, matches[i].Visitor, matches[i].Home, matches[i].Round + teams.Count()-1));
			}
		}

		foreach (var item in matches)
		{
			await CreateMatchSend(item);
		}

		return matches;
	}

	private async Task RotateTeams(List<Team> teams)
    {
        Team temp = teams[teams.Count - 1];
        for (int i = teams.Count - 1; i > 1; i--)
        {
            teams[i] = teams[i - 1];
        }
        teams[1] = temp;
    }
	private static void Shuffle<T>(IList<T> list)
    {
        var rng = new Random();
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
	
	private async Task<int> CreateClassificationSend(Classification classification)
		=> await _dbService.EditData(
				"INSERT INTO classifications (Points, TeamId, ChampionshipId, Position) VALUES (@Points, @TeamId, @ChampionshipId, @Position) returning id", 
				classification
				);
	public async Task<List<Match>> CreateGroupStage(int championshipId)
	{
		if (!await CheckIfChampionhipExists(championshipId))
			throw new ApplicationException("Campeonato passado não existe.");
		
		var championship = await GetByIdSend(championshipId);
		var teams = await GetAllTeamsOfChampionshipSend(championshipId);

		if (championship.TeamQuantity != teams.Count)
			throw new ApplicationException("Campeonato passado com quantidade inválida de times.");
		
		if (championship.Format != Format.GroupStage)
			throw new ApplicationException("Campeonato passado não apresenta o formato de eliminatórias com fase de grupos.");
		
		Shuffle(teams);
		var position = 1;

		foreach (var t in teams)
		{
			if (position == 5)
				position = 1;
			
			await CreateClassificationSend(new Classification(0, t.Id, championshipId, position));
			position++;
		}

		var matches = new List<Match>();
		for (var i = 0; i < teams.Count; i++)
        {
            for (var j = i + 1; j < teams.Count; j++)
            {
				double calculation = i/4;
				double calculation2 = j/4;
				
				if (Math.Ceiling(calculation) != Math.Ceiling(calculation2))
					break;
				
				matches.Add(new Match(championshipId, teams[i].Id, teams[j].Id, 1));
            }
        }

		for (var i = 0; i < matches.Count; i++)
		{
			for(var j = i + 1; j < matches.Count - 1; j++ )
			{
				if (
				(matches[i].Home == matches[j].Home || matches[i].Home == matches[j].Visitor
				|| matches[i].Visitor == matches[j].Home || matches[i].Visitor == matches[j].Visitor)
				&& matches[i].Round == matches[j].Round)
				{
					matches[j].Round++;
				}
			}
		}
		
		// matches.Sort((x, y) => x.Round.CompareTo(y.Round));
		if(championship.DoubleMatchGroupStage)
		{
			var quantityMatches = matches.Count;
			for (var i = 0; i < quantityMatches; i++)
				matches.Add(new Match(championshipId, matches[i].Visitor, matches[i].Home, matches[i].Round + 3));
			
		}

		foreach (var item in matches)
			await CreateMatchSend(item);
		
		return matches;
	}

	public async Task<List<Match>> CreateKnockoutToGroupStageValidationAsync(List<int> teamsId, int championshipId)
	{
		var championship = await GetByIdSend(championshipId);
		var matches = new List<Match>();
		var number = 64;
		var phase = Phase.ThirtySecondOfFinal;
		var teamQuantityInitial = teamsId.Count()/2;
		
		for (int i = 1; i < 7; i++)
		{
			if(!(teamsId.Count() == number))
			{
				phase++;
				number = number / 2;
			} 
		}

		var aux = 1;		
		for (int i = 0; i < teamQuantityInitial; i++)
		{
			if(aux == 1)
			{
				matches.Add(new Match(championshipId, teamsId[0], teamsId[(teamsId.Count()/2)+1], phase));
				var j = teamsId.Count()/2;
				teamsId.RemoveAt(0);
				teamsId.RemoveAt(j);
				aux = 2;
			}

			else
			{
				matches.Add(new Match(championshipId, teamsId[0], teamsId[teamsId.Count()/2], phase));
				var j = (teamsId.Count()/2)-1;
				teamsId.RemoveAt(0);
				teamsId.RemoveAt(j);
				aux = 1;
			}
		}

		var k = 0;
		for (int i = 0; i < matches.Count/2; i += 2)
		{
			var auxiliar = matches[matches.Count/2 + k];
			matches[matches.Count/2 + k] = matches[i+1];
			matches[i+1] = auxiliar;
			k = k + 2;
		}

		if((championship.DoubleMatchEliminations && phase != Phase.Finals) ||
			(championship.FinalDoubleMatch && phase == Phase.Finals))
		{
			var matchesQuantity = matches.Count();

			for(int i = 0; i < matchesQuantity; i++)
			{
				var match = await CreateMatchSend(matches[i]);
				matches.Add(await CreateMatchSend2(new Match(matches[i].ChampionshipId, matches[i].Visitor, matches[i].Home, phase, match.Id)));
			}
		}

		else
		{
			foreach (var item in matches)
			{
				await CreateMatchSend(item);
			}
		}
		return matches;
	}

	public async Task DeleteBracketing(int championshipId)
	{
		var championship = await GetByIdSend(championshipId);

		if(championship.Status != ChampionshipStatus.Pendent && championship.Status != ChampionshipStatus.Inactive)
			throw new ApplicationException("Não é possível deletar um campeonato que já foi iniciado.");
		
		if(championship.Format != Format.Knockout)
		{
			await DeleteClassificationsByChampionshipId(championshipId);
		}

		if(championship.Status == ChampionshipStatus.Inactive)
			await _dbService.EditData("UPDATE championships SET Status = 3 WHERE id = @id", new {id = championship.Id});

		await DeleteMatchesByChampionshipId(championshipId);
	}
	private async Task DeleteMatchesByChampionshipId(int championshipId)
		=> await _dbService.EditData("DELETE FROM Matches WHERE ChampionshipId = @championshipId", new {championshipId});
	private async Task DeleteClassificationsByChampionshipId(int championshipId)
		=> await _dbService.EditData("DELETE FROM Classifications WHERE ChampionshipId = @championshipId", new {championshipId});
	private async Task<Championship> GetByIdSend(int id) 
			=> await _dbService.GetAsync<Championship>("SELECT format, status, teamquantity, DoubleMatchGroupStage, DoubleMatchEliminations, DoubleStartLeagueSystem, FinalDoubleMatch FROM championships WHERE id = @id", new { id });
	public async Task<bool> BracketingExists(int championshipId)
	=> await _dbService.GetAsync<bool>("SELECT EXISTS(SELECT * FROM Matches WHERE championshipId = @championshipId)", new {championshipId});	
}

