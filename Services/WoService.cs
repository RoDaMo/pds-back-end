using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class WoService
{
    private readonly TeamService _teamService;
    public WoService(TeamService teamService)
    {
        _teamService = teamService;
    }

    public async Task DeleteTeamValidation(int teamId, Guid userId)
    {
        await _teamService.DeleteTeamValidation(teamId, userId);
    }
}