using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Microsoft.AspNetCore.Authorization;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/userconfigurations")]
public class UserConfigurationController : ApiBaseController
{
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    public UserConfigurationController(AuthService authService, ErrorLogService error)
    {
        _authService = authService;
        _error = error;
    }
    
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] User user)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            result = await _authService.UpdateProfileValidationAsync(user, userId);
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
    [Route("/userconfigurations/updatepassword")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDTO updatePasswordDTO)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            result = await _authService.UpdatePasswordValidationAsync(updatePasswordDTO, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }
}