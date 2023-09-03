using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Resources.Services;
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
    private readonly Services.ChampionshipService _championshipService;
    private readonly Services.TeamService _teamService;
    private readonly Services.AuthService _authService;
    private readonly ErrorLogService _error;
    private readonly Services.PlayerTempProfileService _playerTempService;
    /// <inheritdoc />
    
    public ModerationController(Services.ChampionshipService championshipService, ErrorLogService error, 
    Services.TeamService teamService, Services.AuthService authService, Services.PlayerTempProfileService playerTempService)
    {
        _championshipService = championshipService;
        _teamService = teamService;
        _authService = authService;
         _playerTempService = playerTempService;
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
            Console.WriteLine("qual foi");        
            await _teamService.DeleteTeamValidation(id);
            Console.WriteLine("chegou");
            return ApiOk(Resource2.TeamDeleted);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
	/// Usado para excluir um usuário.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /moderation/teams/{id}
	///		
	/// </remarks>
	/// <response code="200">Exclui o usuário.</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpDelete]
    [Route("/moderation/users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {        
            await _authService.DeleteCurrentUserValidation(id);
            return ApiOk(Resource3.DeleteUsuarioExcluidoComSucesso);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource3.DeleteHouveErroExcluirUsuario);
        }
    }

    /// <summary>
	/// Usado para excluir um jogador temporário.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /moderation/teams/{id}
	///		
	/// </remarks>
	/// <response code="200">Exclui o jogador temporário.</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpDelete]
    [Route("/moderation/playertempprofiles/{id:guid}")]
    public async Task<IActionResult> DeletePlayerTemp(Guid id)
    {
        try
        {        
            await _playerTempService.DeletePlayerTempValidation(id);
            return ApiOk(Resource3.DeleteUsuarioExcluidoComSucesso);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}