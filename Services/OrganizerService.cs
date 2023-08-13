using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class OrganizerService
{
    private readonly DbService _dbService;
    public OrganizerService(DbService dbService)
    {
        _dbService = dbService;
    }

    public async Task InsertValidation(Organizer model) => await InsertSend(model);

    private async Task InsertSend(Organizer model) => await _dbService.EditData("INSERT INTO organizers (organizerId, championshipId, mainOrganizer) VALUES (@OrganizerId, @ChampionshipId, @MainOrganizer)", model);

    public async Task DeleteValidation(Organizer model) => await DeleteSend(model);

    private async Task DeleteSend(Organizer model) => await _dbService.EditData("DELETE FROM organizers WHERE championshipid = @championshipId AND organizerid = @organizerId", model);

    public async Task<Organizer> IsUserAnOrganizerValidation(Organizer model) => await IsUserAnOrganizerSend(model);

    private async Task<Organizer> IsUserAnOrganizerSend(Organizer model) => await _dbService.GetAsync<Organizer>("SELECT championshipid, organizerid, mainorganizer FROM organizers WHERE organizerid = @organizerId AND championshipid = @championshipId", model);

    public async Task<Organizer> IsUserAnOrganizerValidation(Guid id) => await IsUserAnOrganizerSend(id);

    private async Task<Organizer> IsUserAnOrganizerSend(Guid id) => 
        await _dbService.GetAsync<Organizer>(
            "SELECT cu.championshipid,cu.organizerid,cu.mainorganizer,c.status AS ChampionshipStatus FROM organizers cu LEFT JOIN championships c ON cu.championshipid = c.id WHERE cu.organizerid = @organizerId AND (c.status = 0 OR c.status = 3) AND c.deleted = false;", 
            new { organizerId = id }
        );

    public async Task<bool> IsOrganizerAnywhereValidation(Guid id) => await IsOrganizerAnywhereSend(id);

    private async Task<bool> IsOrganizerAnywhereSend(Guid id) => await _dbService.GetAsync<bool>("SELECT EXISTS (SELECT * FROM organizers WHERE organizerid = @organizerId)", new { organizerId = id });

    public async Task<List<User>> GetAllOrganizersValidation(int championshipId) => await GetAllOrganizersSend(championshipId);

    private async Task<List<User>> GetAllOrganizersSend(int championshipId) =>
        await _dbService.GetAll<User>("SELECT u.name, u.id, u.picture, u.username FROM organizers cs INNER JOIN users u ON u.id = cs.organizerid WHERE cs.championshipid = @championshipId", new { championshipId });


    public async Task<List<Championship>> GetAllChampionshipsByOrganizerValidation(Guid id) => await GetAllChampionshipsByOrganizerSend(id);

    private async Task<List<Championship>> GetAllChampionshipsByOrganizerSend(Guid id) =>
        await _dbService.GetAll<Championship>(
            "SELECT c.name, c.logo, c.status, c.sportsid FROM championships c INNER JOIN organizers cu on c.id = cu.championshipid WHERE cu.organizerid = @organizerId AND c.deleted = false",
            new { organizerId = id });
}