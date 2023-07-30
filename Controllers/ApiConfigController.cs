using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;
using GenericError = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

[Route("/api/config")]
[ApiController]
[Authorize(Roles = "admin")]
public class ApiConfigController : ApiBaseController
{
    private readonly ChampionshipService _championshipService;
    private readonly ErrorLogService _error;
    public ApiConfigController(ChampionshipService championshipService, ErrorLogService error)
    {
        _championshipService = championshipService;
        _error = error;
    }

    [HttpPut]
    public async Task<IActionResult> IndexAllChampionships()
    {
        try
        {
            await _championshipService.IndexAllChampionshipsValidation();
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
        }
    }
}