using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Generic = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;
/// <summary>
///Endpoints destinados à manuntenção dos jogadores de um time.
/// </summary>


[Authorize]
[ApiController]
[Route("/players")]
public class PlayerController : ApiBaseController
{
    private readonly PlayerService _playerService;
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    /// <inheritdoc />
    public PlayerController(PlayerService playerService, ErrorLogService error, AuthService authService)
    {
        _playerService = playerService;
        _error = error;
        _authService = authService;
    }

    /// <summary>
	/// Usado para atualizar usuário para torná-lo jogador.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /players
	///		{
    ///         "artisticName": "Mané",
    ///         "number": 12,
    ///         "playerTeamId": 5,
    ///         "playerPosition": 2,
    ///         "iscaptain": false,
    ///         "id": "xxxx-xxxx-xxxx-xxxx"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Usuário passa a ser jogador de um time.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": []
	///		}
	///		
	/// </returns>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] User user)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            result = await _playerService.CreateValidationAsync(user, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

    [HttpDelete]
    [Route("/players/{id:guid}")]
    public async Task<IActionResult> RemovePlayerFromTeam(Guid id)
    {
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _authService.GetUserByIdAsync(userId);
            
            await _playerService.RemovePlayerFromTeamValidation(user.TeamManagementId, id, userId);
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
    
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetByQuery(string username)
    {
        try
        {
            var users = await _authService.GetUsersByUsernameValidation(username);
            return ApiOk(users.Select(s => new { s.Name, s.Picture, s.Id }));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Generic.GenericErrorMessage);
        }
    }
}