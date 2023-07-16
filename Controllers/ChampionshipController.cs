using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/championships")]
public class ChampionshipController : ApiBaseController
{
  private readonly ChampionshipService _championshipService;
  private readonly RedisService _redisService;
  private readonly AuthService _authService;	
  private readonly ErrorLogService _error;

  public ChampionshipController(ChampionshipService championshipService, RedisService redisService, AuthService authService, ErrorLogService error)
  {
    _championshipService = championshipService;
    _redisService = redisService;
    _authService = authService;
    _error = error;
  }

  [Authorize]
  [HttpPost(Name = "create")]
  public async Task<IActionResult> CreateAsync([FromBody] Championship championship)
  {
    var result = new List<string>();

    try
    {
      var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
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
  public async Task<IActionResult> Index([FromQuery] string name = "", Sports sport = Sports.All, DateTime start = new(), DateTime finish = new(), [FromHeader]string pitId = "", [FromHeader]string sort = "")
  {
    try
    {
      (List<Championship> result, long total) results;
      var sortArray = string.IsNullOrEmpty(sort) ? null : sort.Split(',');
      try
      {
        results = await _championshipService.GetByFilterValidationAsync(name, sport, start, finish, pitId, sortArray);
      }
      catch (Exception)
      {
        pitId = string.Empty;
        results = await _championshipService.GetByFilterValidationAsync(name, sport, start, finish, pitId, sortArray);
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
  [Route("/championships/{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    try
    {
      var championship = await _championshipService.GetByIdValidation(id);
      var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
      if (championship.OrganizerId != userId)
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

