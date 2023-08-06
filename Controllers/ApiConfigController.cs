﻿using Microsoft.AspNetCore.Authorization;
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
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    public ApiConfigController(ChampionshipService championshipService, ErrorLogService error, AuthService authService)
    {
        _championshipService = championshipService;
        _error = error;
        _authService = authService;
    }

    [HttpPut]
    [Route("/api/config/championship")]
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
}