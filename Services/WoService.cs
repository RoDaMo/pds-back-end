using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class WoService
{
    private readonly TeamService _teamService;
    public WoService(TeamService teamService)
    {
        _teamService = teamService;
    }

    public async Task DeleteTeamValidation(User user)
    {
        await _teamService.DeleteTeamValidation(user.TeamManagementId, user.Id);
    }
    
}