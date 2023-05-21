using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/teams")]
public class TeamController : ApiBaseController
{
    private readonly TeamService _teamService;
    private readonly RedisService _redisService;

    public TeamController(TeamService teamService, RedisService redisService)
    {
        _teamService = teamService;
        _redisService = redisService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] TeamDTO teamDto)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            result = await _teamService.CreateValidationAsync(teamDto, userId);
            return result.Any() ? ApiOk(result, false) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var result = await _teamService.GetAllValidationAsync();
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

            return result is null ? ApiOk(result, false) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            return ApiBadRequest(ex.Message);
        }
    }
}
