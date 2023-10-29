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
    private readonly FoulService _foulService;
    /// <inheritdoc />
    public MatchController(ErrorLogService error, MatchService matchService, GoalService goalService, PenaltyService penaltyService, FoulService foulService)
    {
        _error = error;
        _matchService = matchService;
        _goalService = goalService;
        _penaltyService = penaltyService;
        _foulService = foulService;
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
    [Authorize]
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
	///		PUT /matches/{id}/end-game-knockout
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
    [Authorize]
    [Route("/matches/{matchId:int}/end-game-knockout")]
    public async Task<IActionResult> EndGameToKnockout(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameToKnockoutValidationAsync(matchId, true);
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
	///		POST /matches/penalties
    ///		{
    ///             "MatchId": 31,
    ///             "PlayerTempId": "c223084a-90ec-471a-af4a-19697aefaba0",
    ///             "TeamId": 6,
    ///             "Converted": false
    ///        }
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
    [Authorize]
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
	///		PUT /matches/{id}/end-game-league-system
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
    [Authorize]
    [Route("/matches/{matchId:int}/end-game-league-system")]
    public async Task<IActionResult> EndGameToLeagueSystem(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameToLeagueSystemValidationAsync(matchId, true);
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
	///		PUT /matches/{id}/end-game-group-stage
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
    [Authorize]
    [Route("/matches/{matchId:int}/end-game-group-stage")]
    public async Task<IActionResult> CreateGroupStage(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.EndGameToGroupStageValidationAsync(matchId, true);
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
	///		PUT /matches
    ///		{
    ///             "Id": 4469,
    ///             "Local": "Bairro do Limoeiro",
    ///             "HomeUniform": "https://mir-s3-cdn-cf.behance.net/project_modules/disp/e5b04979607885.5cc8847e682c5.jpg",
    ///             "VisitorUniform": "https://mir-s3-cdn-cf.behance.net/project_modules/disp/e5b04979607885.5cc8847e682c5.jpg",
    ///             "Date": "2024-08-18T23:18:00Z",
    ///             "Arbitrator": "Daronco"
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
    [Authorize]
    [Route("/matches")]
    public async Task<IActionResult> UpdateMatch([FromBody] Match match)
    {
        var result = new List<string>();

        try
        {
            result = await _matchService.UpdateMatchValidationAsync(match);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    ///<summary>
    /// Usado para iniciar a prorrogação.
	/// </summary>
    /// <param name="matchId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /matches/{matchId}/prorrogation
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
    [Authorize]
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

    ///<summary>
    /// Usado para obter uma partida de acordo com seu id.
	/// </summary>
    /// <param name="matchId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /matches/{matchId}
	///		
	/// </remarks>
	/// <response code="200">Retorna a partida</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
    ///         "succeed": true,
    ///         "results": {
    ///             "id": 4836,
    ///             "homeName": "alex",
    ///             "visitorName": "nome",
    ///              "homeEmblem": "oi",
    ///              "visitorEmblem": "m",
    ///              "homeGoals": 0,
    ///              "visitorGoals": 0,
    ///              "homeWinnigSets": 0,
    ///              "visitorWinnigSets": 0,
    ///              "isSoccer": true,
    ///              "winnerName": null,
    ///              "homeId": 8,
    ///              "visitorId": 10,
    ///              "finished": false,
    ///              "local": "Em algum lugar",
    ///              "arbitrator": "Daronco",
    ///              "date": "2023-09-08T03:00:00Z"
    ///          }
	///		}
	///		
	/// </returns>
    [HttpGet]
    [Route("/matches/{matchId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Show(int matchId)
    {
        try
        {
            var result = await _matchService.GetMatchByIdValidation(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }   
    }

    ///<summary>
    /// Usado para verificar se partida pode iniciar cobrança de pênaltis
	/// </summary>
    /// <param name="matchId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /matches/{matchId}/penalties
	///		
	/// </remarks>
	/// <response code="200">Retorna um valor booleano para a verificação</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": true
	///		}
	///		
	/// </returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/matches/{matchId:int}/penalties")]
    public async Task<IActionResult> CanThereBePenalties(int matchId)
    {
        try
        {
            var result = await _matchService.CanThereBePenalties(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }   
    }

    ///<summary>
    /// Usado para criar uma falta
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /matches/fouls 
	/// 	{
	///			"YellowCard": false,
	///			"PlayerTempId": "e4eaaaf2-72d6-4b1a-8a15-0a92c9e2b635",
	///			"MatchId": 5015,
    ///			"Minutes": 50
	///		}		
	/// </remarks>
	/// <response code="200">Cria a falta</response>
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
    [HttpPost]
    [Authorize]
    [Route("/matches/fouls")]
    public async Task<IActionResult> CreateFoul(Foul foul)
    {
        var result = new List<string>();

        try
        {
            result = await _foulService.CreateFoulValidationAsync(foul);
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
	/// Usado para obter todos os jogadores do time para uma partida.
	/// </summary>
    /// <param name="matchId"></param>
    /// <param name="teamId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /matches/{matchId}/teams/{teamId}/players
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os jogadores do time para uma partida.</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
    /// Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": [
    ///                {
    ///                   "id": "9e6e84d5-28f5-4f46-b05f-85f5f229c52b",
    ///                   "name": null,
    ///                  "artisticName": null,
    ///                   "number": 8,
    ///                   "teamsId": 0,
    ///                  "playerPosition": 1,
    ///                  "isCaptain": false,
    ///                   "picture": null,
    ///                   "username": ""
    ///               }
    ///            ]
	///		}   
	/// </returns>
    [HttpGet]
    [AllowAnonymous]
    [Route("/matches/{matchId:int}/teams/{teamId:int}/players")]
    public async Task<IActionResult> GetAllPlayersValidInTeam(int matchId, int teamId)
    {
        try
        {
            var players = await _matchService.GetAllPlayersValidInTeamValidation(matchId, teamId);
            players = players.OrderBy(u => u.PlayerPosition).ToList();
            return ApiOk(players.Select(m => new { id = m.Id, name = m.Name, artisticName = m.ArtisticName, number = m.Number, teamsId = m.PlayerTeamId, playerPosition = m.PlayerPosition, isCaptain = m.IsCaptain, picture = m.Picture, username = m.Username }));
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }  
    }

    /// <summary>
	/// Usado para adicionar uma súmula à partida.
	/// </summary>
    /// <param name="match"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /matches/add-match-report
    ///     {
    ///         "Id": 4970,
    ///         "MatchReport": "https://imagem.png"    
    ///     }  
	///		
	/// </remarks>
	/// <response code="200">Adiciona súmula a uma partida.</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
    ///   Exemplo de retorno:
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": ""
	///		}
	/// </returns>
    [HttpPut]
    [Authorize]
    [Route("/matches/add-match-report")]
    public async Task<IActionResult> AddMatchReport(Match match)
    {
        try
        {
            await _matchService.AddMatchReportValidation(match);
            return ApiOk();
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }  
    }

    /// <summary>
	/// Usado para obter todos os eventos de uma partida.
	/// </summary>
    /// <param name="matchId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /matches/{matchId:int}/get-all-events 
	///		
	/// </remarks>
	/// <response code="200">Retorna todos os eventos de uma partida</response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
    ///   Exemplo de retorno:
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": [
    ///		        {
    ///                 "name": "expulso",
    ///                 "playerId": "4475161a-1db9-49b4-b05f-2b153363b283",
    ///                 "converted": false,
    ///                 "teamId": 12,
    ///                 "goal": false,
    ///                 "foul": false,
    ///                 "penalty": true
    ///             }	    
    ///          ]
	///		}
	/// </returns>
    [HttpGet]
    [Route("/matches/{matchId:int}/get-all-events")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllEvents(int matchId)
    {
        try
        {
            var result = await _matchService.GetAllEventsValidation(matchId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }  
    }

    /// <summary>
	/// Usado para terminar a partida em WO.
	/// </summary>
    /// <param name="matchId"></param>
    /// <param name="teamId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /matches/{matchId:int}/teams/{teamId:int}/wo
	///		
	/// </remarks>
	/// <response code="200">termina a partida em WO. </response>
	/// <response code="400">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
    ///   Exemplo de retorno:
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": ""
	///		}
	/// </returns>
    [HttpPut]
    [Route("/matches/{matchId:int}/teams/{teamId:int}/wo")]
    public async Task<IActionResult> WO(int matchId, int teamId)
    {
        try
        {
            await _matchService.WoValidation(matchId, teamId, true);
            return ApiOk();
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }  
    }

    ///<summary>
    /// Usado para iniciar as cobranças de pênaltis da partida.
	/// </summary>
    /// <param name="matchId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /matches/{matchId}/penalties
	///		
	/// </remarks>
	/// <response code="200">Inicia as cobranças de pênaltis da partida</response>
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
    [Authorize]
    [Route("/matches/{matchId:int}/penalties")]
    public async Task<IActionResult> Penalties(int matchId)
    {
        var result = new List<string>();

        try
        {
            await _matchService.ActivePenaltiesValidationAsync(matchId);
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