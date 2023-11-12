using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class FirstStringService
{
    private readonly DbService _dbService;
    private readonly MatchService _matchService;
    private readonly TeamService _teamService;
    private readonly OrganizerService _organizerService;
    private readonly PlayerTempProfileService _playerTempProfileService;
    public FirstStringService(DbService dbService, MatchService matchService, TeamService teamService, OrganizerService organizerService, PlayerTempProfileService playerTempProfileService)
    {
        _dbService = dbService;
        _matchService = matchService;
        _teamService = teamService;
        _organizerService = organizerService;
        _playerTempProfileService = playerTempProfileService;
    }

    public async Task InsertFirstStringPlayerValidation(FirstStringPlayer newFirstString, Guid organizerId)
    {
        var match = await _matchService.GetMatchById(newFirstString.MatchId);
        if (await _organizerService.IsUserAnOrganizerValidation(new Organizer { ChampionshipId = match.ChampionshipId, OrganizerId = organizerId }) is null)
            throw new ApplicationException("Você não possui permissão para alterar as táticas dessa partida");

        if (newFirstString.PlayerId is null)
            throw new ApplicationException("Informe o ID do jogador!");
        
        var tempOrUser = await _playerTempProfileService.GetTempPlayerById(newFirstString.PlayerId.Value);
        if (tempOrUser is not null)
        {
            newFirstString.PlayerTempId = newFirstString.PlayerId;
            newFirstString.PlayerId = null;
        }

        var validPlayers = await _matchService.GetAllPlayersValidInTeamValidation(newFirstString.MatchId, newFirstString.TeamId);
        if (!validPlayers.Any())
            throw new ApplicationException("Não há jogadores válidos nesse time para esta partida!");
        
        if (validPlayers.All(a => a.Id != newFirstString.PlayerId && a.Id != newFirstString.PlayerTempId))
            throw new ApplicationException("Jogador não é válido para vinculo como titular nessa partida.");

        if ((newFirstString.Position > 6 || newFirstString.Position < 0 || newFirstString.Line > 4 || newFirstString.Line < 0) && newFirstString.Line != 99 && newFirstString.Position != 99)
            throw new ApplicationException("Posição do jogador inválida!");
        
        var team = await _teamService.GetByIdValidationAsync(newFirstString.TeamId);
        var hasMatchStarted = await _matchService.HasMatchStarted(newFirstString.MatchId, match);
        
        if (match.Tied || match.Winner > 0)
            throw new ApplicationException("Não é permitido alterar o esquema tático após a finalização da partida.");

        if (hasMatchStarted != string.Empty)
            throw new ApplicationException(hasMatchStarted);

        if (team is null)
            throw new ApplicationException("Time inválido");
        
        await DeleteFromPlayerToUpdate(newFirstString);
        await InsertFirstStringPlayerSend(newFirstString);
    }

    private async Task InsertFirstStringPlayerSend(FirstStringPlayer player) =>
        await _dbService.EditData(
            "INSERT INTO FirstStringPlayers (PlayerId, PlayerTempId, MatchId, TeamId, Position, Line) VALUES (@PlayerId, @PlayerTempId, @MatchId, @TeamId, @Position, @Line)",
            player);

    public async Task InsertPlayersAsSecondStringMassValidation(int teamId, int matchId)
    {
        var players = await _matchService.GetAllPlayersValidInTeamValidation(matchId, teamId);
        if (!players.Any())
            throw new ApplicationException("Não há jogadores válidos nesse time para esta partida!");

        foreach (var player in players)
        {
            var isPlayerTemp = player.Username is null;
            await InsertFirstStringPlayerSend(new FirstStringPlayer
            {
                Line = 0, 
                Position = 0,
                MatchId = matchId, 
                TeamId = teamId, 
                PlayerId = isPlayerTemp ? null : player.Id,
                PlayerTempId = isPlayerTemp ? player.Id : null
            });
        }
    }

    private async Task DeleteFromPlayerToUpdate(FirstStringPlayer player) =>
        await _dbService.EditData(
            "DELETE FROM firststringplayers WHERE matchid = @MatchId AND teamid = @TeamId AND (playertempid = @PlayerTempId OR playerId = @PlayerId)",
            player);

    public async Task<List<User>> GetAllFirstAndSecondStringsValidation(int teamId, int matchId)
    {
        var players = await GetAllFirstAndSecondStringsSend(teamId, matchId);
        var match = await _matchService.GetMatchById(matchId);
        var newPlayers = new List<User>();

        foreach (var player in players)
            if (!await _matchService.CheckIfIsSuspended(player, match))
                newPlayers.Add(player);

        return newPlayers;
    }

    private async Task<List<User>> GetAllFirstAndSecondStringsSend(int teamId, int matchId) 
        => await _dbService.GetAll<User>(@"
            SELECT ptp.id, ptp.name, ptp.artisticname, ptp.number, ptp.email, ptp.teamsid as playerteamid, ptp.playerposition, false as iscaptain, ptp.picture, null as username, fsp.line, fsp.position
            FROM playertempprofiles ptp
            INNER JOIN firstStringPlayers fsp
            ON fsp.playertempid = ptp.id
            WHERE ptp.teamsid = @id AND fsp.matchid = @matchId
            UNION ALL
            SELECT u.id, u.name, u.artisticname, u.number, u.email, u.playerteamid, u.playerposition, u.iscaptain, u.picture, u.username, fsp.line, fsp.position
            FROM users u
            INNER JOIN firstStringPlayers fsp
            ON fsp.playerid = u.id
            WHERE u.playerteamid = @id AND fsp.matchid = @matchId;", new { id = teamId, matchId });
}