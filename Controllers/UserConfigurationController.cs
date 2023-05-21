using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/userconfigurations")]
public class UserConfigurationController : ApiBaseController
{
    private readonly AuthService _authService;
    public UserConfigurationController(AuthService authService)
    {
        _authService = authService;
    }
    
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] User user)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            
            result = await _authService.UpdateProfileValidationAsync(user, userId);
            return result.Any() ? ApiOk(result, false) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
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
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            result = await _authService.UpdatePasswordValidationAsync(updatePasswordDTO, userId);
            return result.Any() ? ApiOk(result, false) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }
}