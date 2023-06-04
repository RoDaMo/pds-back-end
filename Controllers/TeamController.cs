using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/teams")]
public class TeamController : ApiBaseController
{
    private readonly TeamService _teamService;
    private readonly RedisService _redisService;
    private readonly ChampionshipService _championshipService;

    public TeamController(TeamService teamService, RedisService redisService, ChampionshipService championshipService)
    {
        _teamService = teamService;
        _redisService = redisService;
        _championshipService = championshipService;
    }

    [HttpPost]
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
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery]string query, Sports sport)
    {
        try
        {
            var result = string.IsNullOrEmpty(query) ? await _teamService.GetAllValidationAsync(sport) : await _teamService.SearchTeamsValidation(query, sport);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        try
        {
            var result = await _teamService.GetByIdValidationAsync(id);

            return result is null ? ApiBadRequest("Time não existente") : ApiOk(result);
        }
        catch (ApplicationException ex)
        {
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
                return ApiBadRequest("Você não tem permissão para adicionar um time participante.");

            await _teamService.AddTeamToChampionshipValidation(championshipTeamsDto.TeamId, championshipTeamsDto.ChampionshipId);
            return ApiOk("Time vinculado com sucesso");
        }
        catch (ApplicationException e)
        {
            return ApiBadRequest(e.Message);
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
                return ApiBadRequest("Você não tem permissão para remover um time participante.");

            await _teamService.RemoveTeamFromChampionshipValidation(championshipTeamsDto.TeamId, championshipTeamsDto.ChampionshipId);
            return ApiOk("Time desvinculado com sucesso");
        }
        catch (ApplicationException e)
        {
            return ApiBadRequest(e.Message);
        }
    }
}
