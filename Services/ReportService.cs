using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class ReportService
{
    private readonly ChampionshipService _championshipService;
    private readonly TeamService _teamService;
    private readonly AuthService _authService;
    private readonly PlayerTempProfileService _tempProfileService;
    private readonly DbService _dbService;
    public ReportService(ChampionshipService championshipService, TeamService teamService, AuthService authService, PlayerTempProfileService tempProfileService, DbService dbService)
    {
        _championshipService = championshipService;
        _teamService = teamService;
        _authService = authService;
        _tempProfileService = tempProfileService;
        _dbService = dbService;
    }

    public async Task CreateReportValidation(Report report)
    {
        if (report.Description is null)
            throw new ApplicationException("Informe o motivo pela denúncia.");

        var previousReports = await GetReportsFromUserValidation(report.AuthorId);
        if (previousReports.Find(f =>
                (f.ReportedChampionshipId is not null && f.ReportedChampionshipId == report.ReportedChampionshipId) ||
                (f.ReportedTeamId is not null && f.ReportedTeamId == report.ReportedTeamId) ||
                (f.ReportedUserId is not null && f.ReportedUserId == report.ReportedUserId)) is not null)
            throw new ApplicationException("Você pode denunciar um conteúdo apenas uma vez.");
        
        switch (report.ReportType)
        {
            case ReportType.ChampionshipReport:
                if (report.ReportedChampionshipId is null)
                    throw new ApplicationException("Informe o ID do campeonato");
                
                var reportedChampionship = await _championshipService.GetByIdValidation(report.ReportedChampionshipId.Value);
                if (report.ReportedChampionshipId is 0 || reportedChampionship is null)
                    throw new ApplicationException("Campeonato denunciado não é válido");
                
                report.ReportedTeamId = null;
                report.ReportedUserId = null;
                await CreateReportSend(report);
                break;
            case ReportType.TeamReport:
                if (report.ReportedTeamId is null)
                    throw new ApplicationException("Informe o ID do time");
                
                var reportedteam = await _teamService.GetByIdValidationAsync(report.ReportedTeamId.Value);
                if (report.ReportedTeamId is 0 || reportedteam is null)
                    throw new ApplicationException("Time denunciado não é válido");

                report.ReportedChampionshipId = null;
                report.ReportedUserId = null;
                await CreateReportSend(report);
                break;
            case ReportType.UserReport:
                if (report.ReportedUserId is null)
                    throw new ApplicationException("Informe o ID do usuário");
                
                if (report.AuthorId == report.ReportedUserId)
                    throw new ApplicationException("Você não pode denunciar à si mesmo.");
                
                var reportedUser = await _authService.GetUserByIdAsync(report.ReportedUserId.Value);
                var reportedTempUser = await _tempProfileService.GetTempPlayerById(report.ReportedUserId.Value);
                if (report.ReportedUserId == Guid.Empty || (reportedUser is null && reportedTempUser is null))
                    throw new ApplicationException("Usuário denunciado não é válido");

                report.ReportedTeamId = null;
                report.ReportedChampionshipId = null;
                await CreateReportSend(report);
                break;
            case ReportType.All:
            default:
                throw new ApplicationException("Tipo de denúncia inválido");
        }
    }

    private async Task CreateReportSend(Report report) 
        => await _dbService.EditData("INSERT INTO Reports (AuthorId, Completed, Description, reporttype, ReportedUserId, ReportedTeamId, ReportedChampionshipId) VALUES (@AuthorId, @Completed, @Description, @ReportType, @ReportedUserId, @ReportedTeamId, @ReportedChampionshipId)", report);

    public async Task<List<Report>> GetAllByTypeValidation(ReportType type, bool completed) => await GetAllByTypeSend(type, completed);

    private async Task<List<Report>> GetAllByTypeSend(ReportType type, bool completed) 
        => await _dbService.GetAll<Report>($"SELECT id, authorid, completed, description, reporttype, reporteduserid, reportedteamid, reportedchampionshipid FROM Reports WHERE {(type == ReportType.All ? "" : "reporttype = @type AND ")}completed = @completed", new { type, completed });

    public async Task<Report> GetByIdValidation(int id) => await GetByIdSend(id);

    private async Task<Report> GetByIdSend(int id) => await _dbService.GetAsync<Report>("SELECT id, authorid, completed, description, reporttype, reporteduserid, reportedteamid, reportedchampionshipid FROM Reports WHERE Id = @id", new { id });

    public async Task SetReportAsCompletedValidation(int id, bool newStatus) => await SetReportAsCompletedSend(id, newStatus);

    private async Task SetReportAsCompletedSend(int id, bool newStatus) => await _dbService.EditData("UPDATE Reports SET Completed = @newStatus WHERE Id = @id", new { newStatus, id});

    public async Task<List<Report>> GetReportsFromUserValidation(Guid id) => await GetReportsFromUserSend(id);

    private async Task<List<Report>> GetReportsFromUserSend(Guid id) => await _dbService.GetAll<Report>("SELECT id, authorid, completed, description, reporttype, reporteduserid, reportedteamid, reportedchampionshipid FROM Reports WHERE authorid = @id", new { id });
}