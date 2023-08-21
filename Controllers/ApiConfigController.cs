using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;
using GenericError = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoints restritos à admins, com o proposito de administrar a API.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
[Route("/api/config")]
[ApiController]
[Authorize(Roles = "admin")]
public class ApiConfigController : ApiBaseController
{
    private readonly ChampionshipService _championshipService;
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    private readonly IBackgroundJobsService _backgroundService;

    /// <inheritdoc />
    public ApiConfigController(ChampionshipService championshipService, ErrorLogService error, AuthService authService, ImageService imageService, IBackgroundJobsService backgroundService)
    {
        _championshipService = championshipService;
        _error = error;
        _authService = authService;
        _backgroundService = backgroundService;
    }

    [HttpPut]
    [Route("/api/config/championship")]
#pragma warning disable CS1591
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

    [HttpPut]
    [Route("/api/config/user")]
    public async Task<IActionResult> IndexAllUsers()
    {
        try
        {
            await _authService.IndexAllUsersValidation();
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
        } 
    }

    [HttpPut]
    [Route("/api/config/images")]
    public async Task<IActionResult> DownloadAllImagesFromAWS()
    {
        try
        {
            await _backgroundService.EnqueueJob(() => _backgroundService.DownloadFilesFromS3());
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
        } 
    }
#pragma warning restore CS1591
}