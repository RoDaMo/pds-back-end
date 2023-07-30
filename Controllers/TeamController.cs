using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Controllers.TeamController;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/teams")]
public class TeamController : ApiBaseController
{
    private readonly TeamService _teamService;
    private readonly ChampionshipService _championshipService;
    private readonly ErrorLogService _error;
    private readonly ChampionshipActivityLogService _activityLogService;
    public TeamController(TeamService teamService, ChampionshipService championshipService, ErrorLogService error)
    {
        _teamService = teamService;
        _championshipService = championshipService;
        _error = error;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAsync([FromBody] TeamDTO teamDto)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            result = await _teamService.CreateValidationAsync(teamDto, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery]string query, Sports sport, int championshipId)
    {
        try
        {
            var result = string.IsNullOrEmpty(query) ? await _teamService.GetAllValidationAsync(sport) : await _teamService.SearchTeamsValidation(query, sport, championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        try
        {
            var result = await _teamService.GetByIdValidationAsync(id);

            return result is null ? ApiBadRequest(Resource.ShowTimeNaoExistente) : ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpPost]
    [Authorize]
    [Route("/teams/championship")]
    public async Task<IActionResult> AddTeamToChampionship([FromBody]ChampionshipTeamsDto championshipTeamsDto)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(championshipTeamsDto.ChampionshipId);
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (championship.OrganizerId != userId)
                return ApiBadRequest(Resource.AddTeamToChampionshipNoPermission);

            await _teamService.AddTeamToChampionshipValidation(championshipTeamsDto.TeamId, championshipTeamsDto.ChampionshipId);

            await _activityLogService.InsertValidation(new()
            {
                DateOfActivity = DateTime.UtcNow,
                ChampionshipId = championship.Id,
                TypeOfActivity = TypeOfActivity.AddedTeam,
                OrganizerId = userId
            });
            
            return ApiOk(Resource.AddTeamToChampionshipVinculado);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete]
    [Authorize]
    [Route("/teams/championship")]
    public async Task<IActionResult> RemoveTeamFromChampionship([FromBody]ChampionshipTeamsDto championshipTeamsDto)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(championshipTeamsDto.ChampionshipId);
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (championship.OrganizerId != userId)
                return ApiBadRequest(Resource.RemoveTeamFromChampionshipNoPermissionToDelete);

            await _teamService.RemoveTeamFromChampionshipValidation(championshipTeamsDto.TeamId, championshipTeamsDto.ChampionshipId);
            
            await _activityLogService.InsertValidation(new()
            {
                DateOfActivity = DateTime.UtcNow,
                ChampionshipId = championship.Id,
                TypeOfActivity = TypeOfActivity.RemovedTeam,
                OrganizerId = userId
            });
            
            return ApiOk(Resource.RemoveTeamFromChampionshipUnlinked);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpGet]
    [Route("/teams/championship/{id:int}")]
    public async Task<IActionResult> GetTeamChampionships(int id)
    {
        try
        {
            return ApiOk(await _teamService.GetChampionshipsOfTeamValidation(id));
        }
        catch (ApplicationException ex)  
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpPut]
    [Authorize]
    [Route("/teams")]
    public async Task<IActionResult> UpdateTeam(TeamDTO team)
    {
        var result = new List<string>();
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            result = await _teamService.UpdateTeamValidation(team, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(Resource.TeamSuccessfullyUpdated);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete]
    [Route("/teams/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteTeam(int id)
    {
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            await _teamService.DeleteTeamValidation(id, userId);
            return ApiOk(Resource.TeamDeleted);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpGet]
    [Route("/teams/{id:int}/players")]
    public async Task<IActionResult> GetAllPlayersOfTeam(int id)
    {
        try
        {
            var players = await _teamService.GetPlayersOfTeamValidation(id); 
            return ApiOk(players.Select(m => new { id = m.Id, name = m.Name, artisticName = m.ArtisticName, number = m.Number, teamsId = m.PlayerTeamId, playerPosition = m.PlayerPosition, isCaptain = m.IsCaptain }));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}
