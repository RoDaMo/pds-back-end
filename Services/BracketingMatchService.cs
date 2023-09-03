namespace PlayOffsApi.Services;

public class BracketingMatchService
{
	private readonly MatchService _matchService;
    private readonly BracketingService _bracketingService;
    private readonly GoalService _goalService;

    public BracketingMatchService(DbService dbService)
    {
        _bracketingService = new BracketingService(dbService);
        _goalService = new GoalService(dbService, _bracketingService);
        _matchService = new MatchService(dbService, _bracketingService, _goalService);
    }


    public async Task WoValidation(int matchId, int teamId) => await _matchService.WoValidation(matchId, teamId);
}