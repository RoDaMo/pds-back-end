using PlayOffsApi.DTO;

namespace PlayOffsApi.Services;

public class TodoService
{
    private readonly ChampionshipService _championshipService;
    private readonly BracketingService _bracketingService;
    private readonly MatchService _matchService;
    private readonly OrganizerService _organizerService;
    public TodoService(ChampionshipService championshipService, BracketingService bracketingService, MatchService matchService, OrganizerService organizerService)
    {
        _championshipService = championshipService;
        _bracketingService = bracketingService;
        _matchService = matchService;
        _organizerService = organizerService;
    }
    
    public async Task<Todo> GetTodoListOfChampionship(int championshipId)
    {
        var championship = await _championshipService.GetByIdValidation(championshipId);
        if (championship is null)
            throw new ApplicationException("Campeonato não existe.");

        var organizers = await _organizerService.GetAllOrganizersValidation(championshipId);
        var todoList = new Todo
        {
            CreatedBracketing = await _bracketingService.BracketingExists(championshipId),
            Rules = championship.Rules is not null,
            AddedSuborganizers = organizers.Count > 1,
            AddedEnoughTeams = !await _championshipService.CanMoreTeamsBeAddedValidation(championshipId)
        };

        var matches = await _championshipService.GetAllMatchesByChampionshipValidation(championshipId);
        if (!matches.Any()) return todoList;
        
        matches = matches.Where(m => !m.Finished).ToList();
        var earliestRound = matches.Min(m => m.Round);
        var earliestPhase = matches.Min(m => m.Phase);
        
        todoList.PendentMatches = matches.Where(m => (m.Round != 0 && m.Round == earliestRound) || (m.Phase != 0 && m.Phase == earliestPhase)).ToList();
        return todoList;
    }
}