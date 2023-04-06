using pds_back_end.Models;

namespace pds_back_end.Services;

public class ChampionshipService
{
    private readonly DbService _dbService;

    public ChampionshipService(DbService dbService)
    {
        _dbService = dbService;
    }
    public void CreateValidationAsync(Championship championship)
    {
        CreateSendAsync(championship);
    }

    public async void CreateSendAsync(Championship championship)
    {
        var result = await _dbService.EditData(
                "INSERT INTO championships (id, name, prize, sportsid, initialdate, finaldate) VALUES (@Id, @Name, @Prize, @SportsId, @Initialdate, @Finaldate)",
                championship);
    }
}
