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
    public async Task<IActionResult> Index([FromQuery]string query)
    {
        try
        {
            var result = string.IsNullOrEmpty(query) ? await _teamService.GetAllValidationAsync() : await _teamService.SearchTeamsValidation(query);
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
    public async Task<IActionResult> AddTeamToChampionship(int teamId, int championshipId)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(championshipId);
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (championship.Organizer.Id != userId)
                return ApiBadRequest("Você não tem permissão para adicionar um time participante.");

            await _teamService.AddTeamToChampionshipValidation(teamId, championshipId);
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
    public async Task<IActionResult> RemoveTeamFromChampionship(int teamId, int championshipId)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(championshipId);
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (championship.Organizer.Id != userId)
                return ApiBadRequest("Você não tem permissão para remover um time participante.");

            await _teamService.RemoveTeamFromChampionshipValidation(teamId, championshipId);
            return ApiOk("Time desvinculado com sucesso");
        }
        catch (ApplicationException e)
        {
            return ApiBadRequest(e.Message);
        }
    }
}
