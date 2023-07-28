using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Validations;
using Resource = PlayOffsApi.Resources.Services.PlayerService;

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

		if( ((teams.Count() & (teams.Count() - 1)) != 0) || teams.Count() > 64 ||  teams.Count() == 0)
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

		if(teams.Count() < 4 || teams.Count() > 20 || teams.Count() % 2 != 0)
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

		int totalRodadas = teams.Count() - 1;
        int totalJogosPorRodada = teams.Count() / 2;

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
		var quantidadePartidas = matches.Count();
		matches.Sort((x, y) => x.Round.CompareTo(y.Round));

		for (int i = 0; i < quantidadePartidas; i++)
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
}