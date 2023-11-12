using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class ReplacementService
{
    private readonly DbService _dbService;
    private readonly OrganizerService _organizerService;
    private readonly MatchService _matchService;
    public ReplacementService(DbService dbService, OrganizerService organizerService, MatchService matchService)
    {
        _dbService = dbService;
        _organizerService = organizerService;
        _matchService = matchService;
    }

    public async Task InsertValidation(Replacement replacement, Guid organizerId)
    {
        var match = await _matchService.GetMatchById(replacement.MatchId);
        if (await _organizerService.IsUserAnOrganizerValidation(new Organizer { ChampionshipId = match.ChampionshipId, OrganizerId = organizerId }) is null)
            throw new ApplicationException("Você não possui permissão para realizar substituições nesta partida");
        
        var allReplacementsMatch = await GetAllByMatchValidation(replacement.MatchId);
        allReplacementsMatch = allReplacementsMatch.Where(w => w.TeamId == replacement.TeamId).ToList();
        if (allReplacementsMatch.Count > 5)
            throw new ApplicationException("Este time não pode realizar novas substituições.");

        if (allReplacementsMatch.Any(a => a.ReplacedId == replacement.ReplacerId || a.ReplacedTempId == replacement.ReplacerTempId))
            throw new ApplicationException("Um jogador substituido não pode voltar para o campo.");

        await InsertSend(replacement);
    }

    private async Task InsertSend(Replacement replacement) =>
        await _dbService.EditData(
            "INSERT INTO replacements (replacedid, replacedtempid, replacerid, replacertempid, matchid, teamid) VALUES (@ReplacedId, @ReplacedTempId, @ReplacerId, @ReplacerTempId, @MatchId, @TeamId)",
            replacement);

    public async Task<List<Replacement>> GetAllByMatchValidation(int matchId) => await GetAllByMatchSend(matchId);

    private async Task<List<Replacement>> GetAllByMatchSend(int matchId) => await _dbService.GetAll<Replacement>("SELECT * FROM replacements WHERE matchid = @matchId", new { matchId });
}