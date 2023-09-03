using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class WoService
{
    private readonly TeamService _teamService;
    private readonly AuthService _authService;
    private readonly ChampionshipService _championshipService;
    private readonly BracketingMatchService _bracketingMatchService;
    public WoService(DbService dbService, ElasticService elasticService, string secretKey, string issuer, string audience, 
    RedisService redisService, OrganizerService organizerService, IBackgroundJobsService backgroundJobs)
    {
        _authService = new AuthService(secretKey, issuer, audience, dbService, elasticService, this);
        _championshipService = new ChampionshipService(dbService, elasticService, _authService, backgroundJobs, redisService, organizerService);
        _bracketingMatchService = new BracketingMatchService(dbService);
        _teamService = new TeamService(dbService, elasticService, _authService, _championshipService, _bracketingMatchService);
    }

    public async Task DeleteTeamValidation(User user)
    {
        await _teamService.DeleteTeamValidation(user.TeamManagementId, user.Id);
    }
    
}