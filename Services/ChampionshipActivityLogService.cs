using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class ChampionshipActivityLogService
{
    private readonly DbService _dbService;
    public ChampionshipActivityLogService(DbService dbService)
    {
        _dbService = dbService;
    }

    public async Task InsertValidation(ChampionshipActivityLog log) => await InsertSend(log);

    private async Task InsertSend(ChampionshipActivityLog log) =>
        await _dbService.EditData("INSERT INTO ChampionshipActivityLog (dateofactivity, typeofactivity, championshipid, organizerid) VALUES (@DateOfActivity, @TypeOfActivity, @ChampionshipId, @OrganizerId)", log);

    public async Task<List<ChampionshipActivityLog>> GetAllFromChampionshipValidation(int id) 
        => await GetAllFromChampionshipSend(id);

    private async Task<List<ChampionshipActivityLog>> GetAllFromChampionshipSend(int id) 
        => await _dbService.GetAll<ChampionshipActivityLog>("SELECT id, dateofactivity, typeofactivity, championshipid, organizerid FROM championshipactivitylog WHERE id = @id", new { id });
}