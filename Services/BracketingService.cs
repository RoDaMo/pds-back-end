using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class BracketingService
{
    private readonly DbService _dbService;
	private readonly TeamService _teamService;
	private readonly ChampionshipService _championshipService;


    public BracketingService(DbService dbService, TeamService teamService, ChampionshipService championshipService)
	{
		_dbService = dbService;
		_teamService = teamService;
		_championshipService = championshipService;
	}

	public async Task<List<Match>> CreateSimpleknockoutValidationAsync(int championshipId)
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

		return matches;
	}
	private async Task<Match> CreateMatchSend(Match match)
	{
		var id = await _dbService.EditData(
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase, Round) VALUES(@ChampionshipId, @Home, @Visitor, @Phase, @Round) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
	private async Task<Championship> GetByIdSend(int id) 
		=> await _dbService.GetAsync<Championship>("SELECT format, teamquantity, numberofplayers FROM championships WHERE id = @id", new { id });
	private async Task<List<Team>> GetAllTeamsOfChampionshipSend(int championshipId)
		=> await _dbService.GetAll<Team>("SELECT c.emblem, c.name, c.id FROM teams c JOIN championships_teams ct ON c.id = ct.teamId AND ct.championshipid = @championshipId;", new { championshipId });
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
        for (int i = 0; i < teams.Count(); i++)
        {
            for (int j = i+1; j < teams.Count(); j++)
            {
				matches.Add(new Match(championshipId, teams[i].Id, teams[j].Id, 1));
            }
        }

		for (int i = 0; i < matches.Count(); i++)
		{
			for(int j = i + 1; j < matches.Count() - 1; j++ )
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
		var quantityMatches = matches.Count();
		matches.Sort((x, y) => x.Round.CompareTo(y.Round));

		for (int i = 0; i < quantityMatches; i++)
		{
			matches.Add(new Match(championshipId, matches[i].Visitor, matches[i].Home, matches[i].Round + teams.Count()-1));
		}

		foreach (var item in matches)
		{
			await CreateMatchSend(item);
		}

		return matches;
	}
	private async Task<int> CreateClassificationSend(Classification classification)
		=> await _dbService.EditData(
				"INSERT INTO classifications (Points, TeamId, ChampionshipId, Position) VALUES (@Points, @TeamId, @ChampionshipId, @Position) returning id", 
				classification
				);
	public async Task<List<Match>> CreateGroupStage(int championshipId)
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

		if(championship.Format != Format.GroupStage)
		{
			throw new ApplicationException("Campeonato passado não apresenta o formato de eliminatórias com fase de grupos.");
		}

		teams.Sort((x, y) => string.Compare(x.Name, y.Name));

		int position = 1;

		for (int i = 0; i < teams.Count(); i++)
		{
			if(position == 5)
				position = 1;
			await CreateClassificationSend(new Classification(0, teams[i].Id, championshipId, position));
			position++;
		}

		var matches = new List<Match>();


        for (int i = 0; i < teams.Count(); i++)
        {
            for (int j = i+1; j < teams.Count(); j++)
            {
				double calculation = (i/4);
				double calculation2 = (j/4);
				if(Math.Ceiling(calculation) != Math.Ceiling(calculation2))
				{
					break;
				}
				matches.Add(new Match(championshipId, teams[i].Id, teams[j].Id, 1));
            }
        }

		for (int i = 0; i < matches.Count(); i++)
		{
			for(int j = i + 1; j < matches.Count() - 1; j++ )
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
		var quantityMatches = matches.Count();
		matches.Sort((x, y) => x.Round.CompareTo(y.Round));

		foreach (var item in matches)
		{
			await CreateMatchSend(item);
		}
		return matches;
	}

	public async Task<List<Match>> CreateSimpleknockoutToGroupStageValidationAsync(List<int> teamsId, int championshipId)
	{
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

		foreach (var item in matches)
		{
			await CreateMatchSend(item);
		}
		return matches;
	}
}