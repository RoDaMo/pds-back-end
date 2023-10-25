using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using System.Security.Claims;

namespace PlayOffsApi.Controllers;
/// <summary>
///Endpoints destinados à manuntenção dos jogadores temporários de um time.
/// </summary>

[Authorize]
[ApiController]
[Route("/playertempprofiles")]
public class PlayerTempProfileController : ApiBaseController
{
  private readonly PlayerTempProfileService _playerTempProfileService;
  private readonly RedisService _redisService;
  private readonly ErrorLogService _error;
  /// <inheritdoc />
  public PlayerTempProfileController(PlayerTempProfileService playerTempProfileService, RedisService redisService, ErrorLogService error)
  {
    _playerTempProfileService = playerTempProfileService;
    _redisService = redisService;
    _error = error;
  }

    /// <summary>
    /// Usado para cadastrar jogadores sem conta no sistema.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///		POST /playertempprofiles
    ///		{
    ///			"name": "Sadio Mané",
    ///			"artisticName": "Mané",
    ///			"number": 14,
    ///			"email": "sadio.m@gmail.com",
    ///			"teamsId": 5,
    ///			"playerPosition": 3,
    ///			"picture": "link"
    ///		}
    ///		
    /// </remarks>
    /// <response code="200">Jogador sem conta no sistema é vinculado a um time.</response>
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
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] PlayerTempProfile playerTempProfile)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            result = await _playerTempProfileService.CreateValidationAsync(playerTempProfile, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

  /// <summary>
	/// Usado para confirmar entrada em time.
	/// </summary>
  /// <param name="token"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /playertempprofiles/confirm-entry-to-team-?token={token}
	///		
	/// </remarks>
	/// <response code="200">Confirma entrada do jogador no time</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[HttpGet]
	[Route("/playertempprofiles/confirm-entry-to-team")] 
	public async Task<IActionResult> ConfirmEntry(string token)
	{
		try
		{
      var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
      await _playerTempProfileService.ConfirmEmail(token, userId);
			return ApiOk();
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, "Erro ao confirmar entrada no time");
		}
	}  


}

