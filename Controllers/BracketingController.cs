using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoints destinados para a manuntenção do chaveamento de campeonatos.
/// </summary>

[Authorize]
[ApiController]  
[Route("/bracketing")]
public class BracketingController : ApiBaseController
{
    private readonly BracketingService _bracketingService;
    private readonly ErrorLogService _error;
    private readonly ChampionshipActivityLogService _activityLogService;
    /// <inheritdoc />
    public BracketingController(BracketingService bracketingService, ErrorLogService error, ChampionshipActivityLogService activityLogService)
    {
        _bracketingService = bracketingService;
        _error = error;
        _activityLogService = activityLogService;
    }

	/// <summary>
	/// Usado para gerar chaveamento do formato mata-mata.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /bracketing/knockout
    ///		23
    ///		
	/// </remarks>
	/// <response code="200">Um chavamento de formato mata-mata é criado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": {
    ///               "id": 4461,
    ///               "winner": 0,
    ///               "home": 3,
    ///               "visitor": 4,
    ///               "arbitrator": null,
    ///               "championshipId": 23,
    ///               "date": "0001-01-01T00:00:00",
    ///               "round": 0,
    ///               "phase": 3,
    ///               "tied": false,
    ///               "previousMatch": 0,
    ///               "local": null,
    ///               "homeUniform": null,
    ///               "visitorUniform": null
    ///         }
	///		}
    ///		
	/// </returns>
    [HttpPost]
    [Route("/bracketing/knockout")]
    public async Task<IActionResult> CreateKnockout([FromBody] int championshipId)
    {
        try
        {
            var result = await _bracketingService.CreateKnockoutValidationAsync(championshipId);
            
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            await _activityLogService.InsertValidation(new()
            {
                DateOfActivity = DateTime.UtcNow,
                ChampionshipId = championshipId,
                TypeOfActivity = TypeOfActivity.CreatedBracketing,
                OrganizerId = userId
            });
            
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }

	/// <summary>
	/// Usado para gerar chaveamento do formato pontos corridos.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /bracketing/league-system
    ///		23
    ///		
	/// </remarks>
	/// <response code="200">Um chavamento de formato pontos corridos é criado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": {
    ///               "id": 0,
    ///               "winner": 0,
    ///               "home": 4,
    ///               "visitor": 7,
    ///               "arbitrator": null,
    ///               "championshipId": 23,
    ///               "date": "0001-01-01T00:00:00",
    ///               "round": 1,
    ///               "phase": 0,
    ///               "tied": false,
    ///               "previousMatch": 0,
    ///               "local": null,
    ///               "homeUniform": null,
    ///               "visitorUniform": null
    ///         }
	///		}
    ///		
	/// </returns>
    [HttpPost]
    [Route("/bracketing/league-system")]
    public async Task<IActionResult> CreateLeagueSystem([FromBody] int championshipId)
    {
        var result = new List<Match>();
        try
        {
            result = await _bracketingService.CreateLeagueSystemValidationAsync(championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }

	/// <summary>
	/// Usado para gerar chaveamento do formato fase de grupos.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /bracketing/group-stage
    ///		23
    ///		
	/// </remarks>
	/// <response code="200">Um chavamento de formato fase de grupos é criado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": {
    ///               "id": 0,
    ///               "winner": 0,
    ///               "home": 18,
    ///               "visitor": 12,
    ///               "arbitrator": null,
    ///               "championshipId": 23,
    ///               "date": "0001-01-01T00:00:00",
    ///               "round": 1,
    ///               "phase": 0,
    ///               "tied": false,
    ///               "previousMatch": 0,
    ///               "local": null,
    ///               "homeUniform": null,
    ///               "visitorUniform": null
    ///         }
	///		}
    ///		
	/// </returns>
    [HttpPost]
    [Route("/bracketing/group-stage")]
    public async Task<IActionResult> CreateGroupStage([FromBody] int championshipId)
    {
        var result = new List<Match>();
        try
        {
            result = await _bracketingService.CreateGroupStage(championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }


    /// <summary>
	/// Usado para deletar chaveamentos.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /bracketing/delete/{id}
	///		
	/// </remarks>
	/// <response code="200">Deleta o chaveamento de id passado no parâmetro.</response>
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
    [HttpDelete]
    [Route("/bracketing/delete/{championshipId:int}")]
    public async Task<IActionResult> DeleteBracketing(int championshipId)
    {
        var result = new List<Match>();
        try
        {
            await _bracketingService.DeleteBracketing(championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }

    /// <summary>
	/// Usado para verificar se campeonato já possui um chaveamento criado.
	/// </summary>
    /// <param name="id"></param>
    /// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /bracketing/exists/{id}
	///		
	/// </remarks>
	/// <response code="200">Retorna um valor booleano</response>
	/// <response code="400">Retorna um erro indicando algum erro cometido na requisição</response>
    /// <returns>
	///	Exemplo de retorno:
	/// </returns>
    [HttpGet]
    [Route("/bracketing/exists/{id:int}")]
    public async Task<IActionResult> BracketingExists(int id)
    {
        try
        {
            var result = await _bracketingService.BracketingExists(id);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }
}