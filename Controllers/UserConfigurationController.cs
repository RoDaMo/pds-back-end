using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Microsoft.AspNetCore.Authorization;

namespace PlayOffsApi.Controllers;
/// <summary>
///Endpoints destinados à configuração do usuário.
/// </summary>

[Authorize]
[ApiController]
[Route("/userconfigurations")]
public class UserConfigurationController : ApiBaseController
{
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    /// <inheritdoc />
    public UserConfigurationController(AuthService authService, ErrorLogService error)
    {
        _authService = authService;
        _error = error;
    }
    
    /// <summary>
	/// Usado para atualizar perfil do usuário.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /userconfigurations
	///		{
    ///         "Username": "UsuarioTeste",
    ///         "Bio": "Bio bio bio bio bio",
    ///         "Picture": ""
	///		}
	///		
	/// </remarks>
	/// <response code="200">Perfil do usuário é atualizado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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