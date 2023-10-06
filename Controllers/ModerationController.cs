using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
// using PlayOffsApi.Resources.Services;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Championship;
using Resource2 = PlayOffsApi.Resources.Controllers.TeamController;
using Resource3 = PlayOffsApi.Resources.Controllers.AuthController;



namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoints destinados à moderação de usuários, times e campeonatos.
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
public class ModerationController : ApiBaseController
{
    private readonly ChampionshipService _championshipService;
    private readonly TeamService _teamService;
    private readonly AuthService _authService;
    private readonly ErrorLogService _error;
    private readonly PlayerTempProfileService _playerTempService;
    private readonly WoService _woService;
    private readonly OrganizerService _organizerService;
    /// <inheritdoc />
    
    public ModerationController(ChampionshipService championshipService, ErrorLogService error, TeamService teamService, AuthService authService, PlayerTempProfileService playerTempService, WoService woService, OrganizerService organizerService)
    {
		_championshipService = championshipService;
		_error = error;
		_teamService = teamService;
		_authService = authService;
		_playerTempService = playerTempService;
		_woService = woService;
		_organizerService = organizerService;
    }

    /// <summary>
	/// Usado para excluir um campeonato.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /moderation/championships/{id}
	///		
	/// </remarks>
	/// <response code="200">Exclui o campeonato.</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpDelete]
    [Route("/moderation/championships/{id:int}")]
    public async Task<IActionResult> DeleteChampionship(int id)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(id);
        
            await _championshipService.DeleteValidation(championship);
            return ApiOk(Resource.DeleteDeleted);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
	/// Usado para excluir um time.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /moderation/teams/{id}
	///		
	/// </remarks>
	/// <response code="200">Exclui o time.</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpDelete]
    [Route("/moderation/teams/{id:int}")]
    public async Task<IActionResult> DeleteTeam(int id)
    {
        try
        {
            await _teamService.DeleteTeamValidation(id);
            return ApiOk(Resource2.TeamDeleted);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    ///  <summary>
    ///  Usado para excluir um usuário.
    ///  </summary>
    ///  <param name="id"></param>
    ///  <param name="tempUser"></param>
    ///  <remarks>
    ///  Exemplo de requisição:
    ///  
    /// 		DELETE /moderation/teams?id={id}&amp;tempUser=false
    /// 		
    ///  </remarks>
    ///  <response code="200">Exclui o usuário.</response>
    ///  <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
    ///  <returns>
    ///  </returns>
    [HttpDelete]
    [Route("/moderation/users")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteUser(Guid id, bool tempUser)
    {
        try
        {
	        if (tempUser)
	        {
		        await _playerTempService.DeletePlayerTempValidation(id);
		        return ApiOk(Resource3.DeleteUsuarioExcluidoComSucesso);
	        }
	        
	        var user = await _authService.GetUserByIdAsync(id);
            if (user.TeamManagementId != 0)
				await _woService.DeleteTeamValidation(user.TeamManagementId, user.Id);
            if (user.ChampionshipId != 0)
				await _organizerService.DeleteValidation(new() { ChampionshipId = user.ChampionshipId, OrganizerId = user.Id });
            await _authService.DeleteCurrentUserValidation(user);

            return ApiOk(Resource3.DeleteUsuarioExcluidoComSucesso);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}