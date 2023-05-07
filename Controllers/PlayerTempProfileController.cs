using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using System.Security.Claims;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/playertempprofiles")]
public class PlayerTempProfileController : ApiBaseController
{
  private readonly PlayerTempProfileService _playerTempProfileService;
  private readonly RedisService _redisService;
  public PlayerTempProfileController(PlayerTempProfileService playerTempProfileService, RedisService redisService)
  {
    _playerTempProfileService = playerTempProfileService;
    _redisService = redisService;
  }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] PlayerTempProfile playerTempProfile)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

		    Console.WriteLine(userId);

           

            result = await _playerTempProfileService.CreateValidationAsync(playerTempProfile, userId);
            if (result.Any())
            {
                return ApiOk(result, false);
            }

            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }


}

