using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]  
[Route("/bracketing")]
public class BracketingController : ApiBaseController
{
    private readonly BracketingService _bracketingService;
    private readonly ErrorLogService _error;
    public BracketingController(BracketingService bracketingService, ErrorLogService error)
    {
        _bracketingService = bracketingService;
        _error = error;
    }

    [HttpPost]
    [Route("/bracketing/simple-knockout")]
    public async Task<IActionResult> CreateSimpleknockout([FromBody] int championshipId)
    {
        var result = new List<Match>();
        try
        {
            result = await _bracketingService.CreateSimpleknockoutValidationAsync(championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }

    [HttpPost]
    [Route("/bracketing/league-system")]
    public async Task<IActionResult> CreateLeagueSystem([FromBody] int championshipId)
    {
        var result = new List<Match>();
        try
        {
            result = await _bracketingService.CreateLeagueSystemValidationAsync(championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }
}