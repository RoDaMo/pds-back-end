using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Enum;
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
    private readonly ChampionshipActivityLogService _activityLogService;
    public BracketingController(BracketingService bracketingService, ErrorLogService error, ChampionshipActivityLogService activityLogService)
    {
        _bracketingService = bracketingService;
        _error = error;
        _activityLogService = activityLogService;
    }

    [HttpPost]
    [Route("/bracketing/simple-knockout")]
    public async Task<IActionResult> CreateSimpleknockout([FromBody] int championshipId)
    {
        try
        {
            var result = await _bracketingService.CreateSimpleknockoutValidationAsync(championshipId);
            
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            await _activityLogService.InsertValidation(new()
            {
                DateOfActivity = DateTime.UtcNow,
                ChampionshipId = championshipId,
                TypeOfActivity = TypeOfActivity.CreatedBracketing,
                OrganizerId = userId
            });
            
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

    [HttpPost]
    [Route("/bracketing/simple-knockout-group-stage")]
    public async Task<IActionResult> CreateSimpleKnockoutGroupStage([FromBody] int championshipId)
    {
        var result = new List<Match>();
        try
        {
            result = await _bracketingService.CreateGroupStage(championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }
}