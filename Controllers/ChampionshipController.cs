using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/championships")]
public class ChampionshipController : ApiBaseController
{
  private readonly ChampionshipService _championshipService;
  private readonly OrganizerService _organizerService;
  private readonly AuthService _authService;	
  private readonly ErrorLogService _error;
  private readonly ChampionshipActivityLogService _activityLogService;

  public ChampionshipController(ChampionshipService championshipService, AuthService authService, ErrorLogService error, ChampionshipActivityLogService activityLogService, OrganizerService organizerService)
  {
    _championshipService = championshipService;
    _authService = authService;
    _error = error;
    _activityLogService = activityLogService;
    _organizerService = organizerService;
  }

  [Authorize]
  [HttpPost(Name = "create")]
  public async Task<IActionResult> CreateAsync([FromBody] Championship championship)
  {
    var result = new List<string>();

    try
    {
      var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

      var isOrganizer = await _organizerService.IsUserAnOrganizerValidation(userId);
      if (isOrganizer is not null)
        return ApiBadRequest("Usuário já possui um campeonato ativo ou pendente!");

      var user = await _authService.GetUserByIdAsync(userId);
      championship.Organizer = user;
      championship.OrganizerId = user.Id;
      
      result = await _championshipService.CreateValidationAsync(championship);
      if (result.Any())
        return ApiBadRequest(result);

      result.Add(Resource.ChampionshipAdded);
      return ApiOk(result);
    }
    catch (ApplicationException ex)
    {
      await _error.HandleExceptionValidationAsync(HttpContext, ex);
      result.Add(ex.Message);
      return ApiBadRequest(result);
    }
  }


  [HttpGet(Name = "index")]
  public async Task<IActionResult> Index([FromQuery] string name = "", Sports sport = Sports.All, DateTime start = new(), DateTime finish = new(), ChampionshipStatus status = ChampionshipStatus.Active, [FromHeader]string pitId = "", [FromHeader]string sort = "")
  {
    try
    {
      (List<Championship> result, long total) results;
      var sortArray = string.IsNullOrEmpty(sort) ? null : sort.Split(',');
      try
      {
        results = await _championshipService.GetByFilterValidationAsync(name, sport, start, finish, pitId, sortArray, status);
      }
      catch (Exception)
      {
        pitId = string.Empty;
        results = await _championshipService.GetByFilterValidationAsync(name, sport, start, finish, pitId, sortArray, status);
      }
      var totalPaginas = Math.Ceiling(results.total / 15m);

      return ApiOk(results.result, message: totalPaginas.ToString("N0"));
    }
    catch (ApplicationException ex)
    {
      await _error.HandleExceptionValidationAsync(HttpContext, ex);
      return ApiBadRequest(ex.Message);
    }
  }

  [HttpGet]
  [Route("/championships/{id:int}")]
  public async Task<IActionResult> Show(int id)
  {
    try
    {
      return ApiOk(await _championshipService.GetByIdValidation(id));
    }
    catch (ApplicationException ex)
    {
      await _error.HandleExceptionValidationAsync(HttpContext, ex);
      return ApiBadRequest(Resource.ShowCampeonatoIdNaoExiste);
    }
  }

  [HttpPut]
  [Authorize]
  public async Task<IActionResult> Update([FromBody] Championship championship)
  {
    try
    {
      var results = await _championshipService.UpdateValidate(championship);
      if (results.Any())
        return ApiBadRequest(results);
      
      var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
      
      await _activityLogService.InsertValidation(new()
      {
        DateOfActivity = DateTime.UtcNow,
        ChampionshipId = championship.Id,
        TypeOfActivity = TypeOfActivity.EditedInfo,
        OrganizerId = userId
      });
      
      return ApiOk(Resource.UpdateCampeonatoAtualizadoComSucesso);
    }
    catch (ApplicationException ex)
    {
      await _error.HandleExceptionValidationAsync(HttpContext, ex);
      return ApiBadRequest(ex.Message);
    }
  }

  [HttpGet]
  [Route("/championships/teams")]
  public async Task<IActionResult> GetAllTeams(int championshipId)
  {
    try
    {
      var championships = await _championshipService.GetAllTeamsOfChampionshipValidation(championshipId);
      return ApiOk(championships);
    }
    catch (ApplicationException ex)
    {
      await _error.HandleExceptionValidationAsync(HttpContext, ex);
      return ApiBadRequest(ex.Message);
    }
  }

  [HttpDelete]
  [Authorize]
  [Route("/championships/{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    try
    {
      var championship = await _championshipService.GetByIdValidation(id);
      var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
      
      var organizador = await _organizerService.IsUserAnOrganizerValidation(userId);
      if (organizador is null || !organizador.MainOrganizer)
        return ApiUnauthorizedRequest(Resource.DeleteNotAuthorized);
      
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
	/// Usado para obter as partidas de um campeonato conforme parâmetros.
	/// </summary>
  /// <param name="id"></param>
  /// <param name="round"></param>
  /// <param name="phase"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /championships/{id}/matches/?round={round}&amp;phase={phase}
	///		
	/// </remarks>
	/// <response code="200">Lista de partidas conforme os parâmetros passados.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
  ///      "succeed": true,
  ///      "results": [
  ///        {
  ///                "id": 4828,
  ///                "homeName": "Lagarta",
  ///                "visitorName": "Harry Potter",
  ///                "homeEmblem": "oi",
  ///                "visitorEmblem": "oi",
  ///                "homeGoals": 0,
  ///                "visitorGoals": 4,
  ///                "homeWinnigSets": 0,
  ///                "visitorWinnigSets": 0,
  ///                "isSoccer": true,
  ///                "winnerName": null,
  ///                "homeId": 4,
  ///                "visitorId": 5,
  ///                "finished": true,
  ///                "local": "Em algum lugar",
  ///                "arbitrator": "Daronco",
  ///                "date": "2023-09-08T03:00:00Z"
  ///         }
	///		}
	///		
	/// </returns>
  [HttpGet]
  [Route("/championships/{id:int}/matches")]
  public async Task<IActionResult> GetAllMatchesByRound(int id, [FromQuery] int round, [FromQuery] int phase)
  {
    try
    {
      
      var result = (phase == 0) ? await _championshipService.GetAllMatchesByRoundValidation(id, round) 
      : await _championshipService.GetAllMatchesByPhaseValidation(id, phase);
      return ApiOk(result);
    }
    catch (ApplicationException ex)
    {
      await _error.HandleExceptionValidationAsync(HttpContext, ex);
      return ApiBadRequest(ex.Message);
    }
  }
}

