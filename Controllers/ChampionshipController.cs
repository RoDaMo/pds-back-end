using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Controllers;
/// <summary>
///Endpoints destinados à manuntenção dos times.
/// </summary>

[ApiController]
[Route("/championships")]
public class ChampionshipController : ApiBaseController
{
  private readonly ChampionshipService _championshipService;
  private readonly OrganizerService _organizerService;
  private readonly AuthService _authService;	
  private readonly ErrorLogService _error;
  private readonly ChampionshipActivityLogService _activityLogService;
  /// <inheritdoc />

  public ChampionshipController(ChampionshipService championshipService, AuthService authService, ErrorLogService error, ChampionshipActivityLogService activityLogService, OrganizerService organizerService)
  {
    _championshipService = championshipService;
    _authService = authService;
    _error = error;
    _activityLogService = activityLogService;
    _organizerService = organizerService;
  }

	/// <summary>
	/// Usado para criar um campeonato.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /championships
	///		{
	///			"name": "volei 33",
	///			"initialDate": "2024-01-01",
	///			"finalDate": "2025-01-01",
  ///			"sportsId": 2,
  ///			"teamQuantity": 16,
  ///			"logo": "",
  ///			"description": "hahahahahhahahahhahakkkkkkk",
  ///			"Format": 1,
  ///			"NumberOfPlayers": 800,
  ///			"DoubleStartLeagueSystem": false,
  ///			"DoubleMatchEliminations": false,
  ///			"DoubleMatchGroupStage": false,
  ///			"FinalDoubleMatch": false
	///		}
	///		
	/// </remarks>
	/// <response code="200">Cria um campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": ["Campeonato cadastrado"]
	///		}
	///		
	/// </returns>
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


	/// <summary>
	/// Usado para listar campeonatos conforme parâmetros.
	/// </summary>
  /// <param name="name"></param>
  /// <param name="sport"></param>
  /// <param name="start"></param>
  /// <param name="finish"></param>
  /// <param name="pitId"></param>
  /// <param name="sort"></param>
  /// <param name="status"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /championships?name={name}&amp;sport={sport}&amp;start={start}&amp;finish={finish}&amp;pitId={pitId}&amp;sort={sort}&amp;status={status}
	///		
	/// </remarks>
	/// <response code="200">Lista os campeonatos conforme os parâmetros passados.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": [
  ///			  {
  ///			    "id": 7,
  ///			    "name": "Campeonato",
  ///			    "initialDate": "2024-05-01T00:00:00",
  ///			    "finalDate": "2025-01-01T00:00:00",
  ///			    "sportsId": 2
  ///       }
  ///     ]
	///		}
	///		
	/// </returns>
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

	/// <summary>
	/// Usado para obter campeonato por id.
	/// </summary>
  /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /championships/{id}
	///		
	/// </remarks>
	/// <response code="200">Exibe o campeonato conforme id.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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

	/// <summary>
	/// Usado para atualizar um campeonato.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /championships
	///		{
  ///		  "id": 39,
	///			"name": "volei 33",
	///			"initialDate": "2024-01-01",
	///			"finalDate": "2025-01-01",
  ///			"sportsId": 2,
  ///			"teamQuantity": 16,
  ///			"logo": "",
  ///			"description": "hahahahahhahahahhahakkkkkkk",
  ///			"Format": 1,
  ///			"NumberOfPlayers": 800,
  ///			"DoubleStartLeagueSystem": false,
  ///			"DoubleMatchEliminations": false,
  ///			"DoubleMatchGroupStage": false,
  ///			"FinalDoubleMatch": false
	///		}
	///		
	/// </remarks>
	/// <response code="200">Atualiza o campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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
}

