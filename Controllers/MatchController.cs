using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/matches")]
public class MatchController : ApiBaseController
{
    private readonly ErrorLogService _error;
    private readonly MatchService _matchService;
    public MatchController(ErrorLogService error, MatchService matchService)
    {
        _error = error;
        _matchService = matchService;
    }
    
    [HttpPost]
    [Route("/matches/goals")]
    public async Task<IActionResult> CreateGoals([FromBody] Goal goal)
    {
        var result = new List<string>();

        try
        {
            result = await _matchService.CreateGoalValidationAsync(goal);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    [HttpPut]
    [Route("/matches/end-game")]
    public async Task<IActionResult> EndGame([FromBody] int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameValidationAsync(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }
}