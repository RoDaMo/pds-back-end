using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[Authorize]
[ApiController]
[Route("/matches")]
public class MatchController : ApiBaseController
{
    private readonly ErrorLogService _error;
    private readonly MatchService _matchService;
    private readonly GoalService _goalService;
    private readonly PenaltyService _penaltyService;
    public MatchController(ErrorLogService error, MatchService matchService, GoalService goalService, PenaltyService penaltyService)
    {
        _error = error;
        _matchService = matchService;
        _goalService = goalService;
        _penaltyService = penaltyService;
    }
    
    [HttpPost]
    [Route("/matches/goals")]
    public async Task<IActionResult> CreateGoal([FromBody] Goal goal)
    {
        var result = new List<string>();

        try
        {
            result = await _goalService.CreateGoalValidationAsync(goal);
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
    [Route("/matches/{matchId:int}/end-game-knockout")]
    public async Task<IActionResult> EndGameToKnockout(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameToKnockoutValidationAsync(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    [HttpPost]
    [Route("/matches/penalties")]
    public async Task<IActionResult> CreatePenalty([FromBody] Penalty penalty)
    {
        var result = new List<string>();
        try
        {
            result = await _penaltyService.CreatePenaltyValidationAsync(penalty);
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
    [Route("/matches/{matchId:int}/end-game-league-system")]
    public async Task<IActionResult> EndGameToLeagueSystem(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameToLeagueSystemValidationAsync(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    [HttpPut]
    [Route("/matches/{matchId:int}/end-game-group-stage")]
    public async Task<IActionResult> CreateGroupStage(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameToGroupStageValidationAsync(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    /// <summary>
	/// Usado para atualizar a data e o árbitro da partida
	/// </summary>
	/// <response code="200">Retorna o status de sucesso da requisição</response>
	/// <response code="400">Retorna um erro indicando algum erro cometido na requisição</response>
    [HttpPut]
    [Route("/matches")]
    public async Task<IActionResult> UpdateMatch([FromBody] Match match)
    {
        var result = new List<string>();

        try
        {
            result = await _matchService.UpdateMatchValidationAsync(match);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    /// Usado para iniciar a prorrogação.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /matches/{id}/prorrogation
	///		
	/// </remarks>
	/// <response code="200">Inicia a prorrogação da partida</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
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
    [Route("/matches/{matchId:int}/prorrogation")]
    public async Task<IActionResult> Prorrogation(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.ActiveProrrogationValidationAsync(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }
}