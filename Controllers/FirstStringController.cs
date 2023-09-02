using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Dedicado à jogadores titulares e reservas.
/// </summary>
[ApiController]
[Route("/first-string")]
public class FirstStringController : ApiBaseController
{
    private readonly FirstStringService _firstStringService;
    private readonly ErrorLogService _error;

    public FirstStringController(FirstStringService firstStringService, ErrorLogService errorLog)
    {
        _firstStringService = firstStringService;
        _error = errorLog;
    }
    
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> InsertFirstStringPlayer(FirstStringPlayer firstStringPlayer)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            await _firstStringService.InsertFirstStringPlayerValidation(firstStringPlayer, userId);
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllFirstAndSecondStringPlayers(int matchId, int teamId)
    {
        try
        {
            return ApiOk(await _firstStringService.GetAllFirstAndSecondStringsValidation(teamId, matchId));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}