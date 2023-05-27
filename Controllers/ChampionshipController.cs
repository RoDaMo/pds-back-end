using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using System.Text.Json;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/championships")]
public class ChampionshipController : ApiBaseController
{
  private readonly ChampionshipService _championshipService;
  private readonly RedisService _redisService;
  private readonly AuthService _authService;
  public ChampionshipController(ChampionshipService championshipService, RedisService redisService, AuthService authService)
  {
    _championshipService = championshipService;
    _redisService = redisService;
    _authService = authService;
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
      
      result = await _championshipService.CreateValidationAsync(championship);
      if (result.Any())
        return ApiBadRequest(result);

      result.Add(Resource.ChampionshipAdded);
      return ApiOk(result);
    }
    catch (ApplicationException ex)
    {
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
    catch (ApplicationException)
    {
      return ApiBadRequest("Campeonato com esse ID não existe");
    }
  }
}

