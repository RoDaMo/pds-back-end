using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;


[Authorize]
[ApiController]
[Route("/players")]
public class PlayerController : ApiBaseController
{
    private readonly RedisService _redisService;
    private readonly PlayerService _playerService;
    public PlayerController(RedisService redisService, PlayerService playerService)
    {
        _redisService = redisService;
        _playerService = playerService;
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] User user)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            result = await _playerService.CreateValidationAsync(user, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

}