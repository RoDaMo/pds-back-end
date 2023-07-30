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
    private readonly PlayerService _playerService;
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    public PlayerController(PlayerService playerService, ErrorLogService error, AuthService authService)
    {
        _playerService = playerService;
        _error = error;
        _authService = authService;
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
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

    [HttpDelete]
    [Route("/players/{id:guid}")]
    public async Task<IActionResult> RemovePlayerFromTeam(Guid id)
    {
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _authService.GetUserByIdAsync(userId);
            
            await _playerService.RemovePlayerFromTeamValidation(user.TeamManagementId, id, userId);
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}