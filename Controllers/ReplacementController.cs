using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/replacement")]
public class ReplacementController : ApiBaseController
{
    private readonly ReplacementService _replacementService;
    private readonly ErrorLogService _error;
    public ReplacementController(ErrorLogService error, ReplacementService replacementService)
    {
        _error = error;
        _replacementService = replacementService;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> InsertReplacement(Replacement replacement)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            await _replacementService.InsertValidation(replacement, userId);
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
    public async Task<IActionResult> GetAllReplacementsInMatch(int matchId)
    {
        try
        {
            return ApiOk(await _replacementService.GetAllByMatchValidation(matchId));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}