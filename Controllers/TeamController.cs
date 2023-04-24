using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

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
    public async Task<IActionResult> CreateAsync([FromBody] Team team)
    {
        var result = new List<string>();

        try
        {
            result = await _teamService.CreateValidationAsync(team);
            if (result.Any())
            {
                return ApiOk(result, false);
            }

            // result.Add(Resource.ChampionshipAdded);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }
}
