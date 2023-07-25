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
		var championship = await GetByIdSend(championshipId);
		var teams = await GetAllTeamsOfChampionshipSend(championshipId);

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
			"INSERT INTO matches (ChampionshipId, Home, Visitor, Phase) VALUES(@ChampionshipId, @Home, @Visitor, @Phase) returning id", match
			);
		return await _dbService.GetAsync<Match>("SELECT * FROM matches WHERE id = @id", new { id });
	}
	private async Task<Championship> GetByIdSend(int id) 
		=> await _dbService.GetAsync<Championship>("SELECT format, teamquantity, numberofplayers FROM championships WHERE id = @id", new { id });
	private async Task<List<Team>> GetAllTeamsOfChampionshipSend(int championshipId)
		=> await _dbService.GetAll<Team>("SELECT c.emblem, c.name, c.id FROM teams c JOIN championships_teams ct ON c.id = ct.teamId AND ct.championshipid = @championshipId;", new { championshipId });

}