using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

/// <summary>
///Endpoints destinados à manuntenção de partidas.
/// </summary>
[Authorize]
[ApiController]
[Route("/matches")]
public class MatchController : ApiBaseController
{
    private readonly ErrorLogService _error;
    private readonly MatchService _matchService;
    private readonly GoalService _goalService;
    private readonly PenaltyService _penaltyService;
    /// <inheritdoc />
    public MatchController(ErrorLogService error, MatchService matchService, GoalService goalService, PenaltyService penaltyService)
    {
        _error = error;
        _matchService = matchService;
        _goalService = goalService;
        _penaltyService = penaltyService;
    }

    /// <summary>
	/// Usado para adicionar gol ou ponto.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /matches/goals
	///		{
	///			"MatchId": 542,
	///			"PlayerTempId": "e3b82666-d624-4c09-87df-f330029a402a",
	///			"TeamId": 3,
    ///			"AssisterPlayerTempId": "f5d4d4b8-9e53-40fb-80dc-37ca5b03e6ea"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Gol ou ponto é adicionado.</response>
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

    /// <summary>
	/// Usado para terminar partida de futebol de eliminatórias.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT matches/{id}/end-game-knockout
	///		
	/// </remarks>
	/// <response code="200">Partida eliminatória é finalizada.</response>
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
    [Route("/matches/end-game-knockout")]
    public async Task<IActionResult> EndGameToKnockout([FromBody] int matchId)
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

    /// <summary>
	/// Usado para atribuir gols em disputa de penaltis.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST matches/penalties
    ///		{
    ///             "MatchId": 31,
    ///             "PlayerTempId": "c223084a-90ec-471a-af4a-19697aefaba0",
    ///             "TeamId": 6,
    ///             "Converted": false
    ///         }
	///		
	/// </remarks>
	/// <response code="200">Atribui ou não o gol conforme o atributo "Converted".</response>
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

    /// <summary>
	/// Usado para terminar partida de futebol de pontos corridos.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT matches/{id}/end-game-league-system
	///		
	/// </remarks>
	/// <response code="200">Partida pontos corridos é finalizada.</response>
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
    [Route("/matches/end-game-league-system")]
    public async Task<IActionResult> EndGameToLeagueSystem([FromBody] int matchId)
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

    /// <summary>
	/// Usado para terminar partida de futebol de fase de grupos.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT matches/{id}/end-game-group-stage
	///		
	/// </remarks>
	/// <response code="200">Partida fase de grupos é finalizada.</response>
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
    [Route("/matches/end-game-group-stage")]
    public async Task<IActionResult> CreateGroupStage([FromBody] int matchId)
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
	/// Usado para atualizar informações pertinentes da partida.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT matches
    ///		{
    ///         "Id": 4469,
    ///         "Local": "Bairro do Limoeiro",
    ///         "HomeUniform": "https://mir-s3-cdn-cf.behance.net/project_modules/disp/e5b04979607885.5cc8847e682c5.jpg",
    ///         "VisitorUniform": "https://mir-s3-cdn-cf.behance.net/project_modules/disp/e5b04979607885.5cc8847e682c5.jpg",
    ///         "Date": "2024-08-18T23:18:00Z",
    ///         "Arbitrator": "Daronco"
    ///	    }
	///		
	/// </remarks>
	/// <response code="200">Partida é atualizada.</response>
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
}