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
  public ChampionshipController(ChampionshipService championshipService, RedisService redisService)
  {
    _championshipService = championshipService;
    _redisService = redisService;
  }

  [Authorize]
  [HttpPost(Name = "create")]
  public async Task<IActionResult> CreateAsync([FromBody] Championship championship)
  {
    var result = new List<string>();

    try
    {
      result = await _championshipService.CreateValidationAsync(championship);
      if (result.Any())
        return ApiOk(result, false);

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
  public async Task<IActionResult> Index([FromQuery] string name = "", Sports sport = Sports.All, DateTime start = new(), DateTime finish = new(), [FromHeader]string pitId = "", [FromHeader]string[] sort = null)
  {
    try
    {
      List<Championship> result;
      await using var redisDb = await _redisService.GetDatabase();
      var cachePagina = await redisDb.GetAsync<string>(name);

      if (!string.IsNullOrEmpty(cachePagina) && sport == Sports.All && start == DateTime.MinValue && finish == DateTime.MinValue)
        result = JsonSerializer.Deserialize<List<Championship>>(cachePagina);
      else
      {
        result = await _championshipService.GetByFilterValidationAsync(name, sport, start, finish, pitId, sort);
        await redisDb.SetAsync(name, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(20));
      }

      return ApiOk(result, message: result.Last().PitId);
    }
    catch (ApplicationException ex)
    {
      return ApiBadRequest(ex.Message);
    }
  }

  [HttpGet]
  [Route("/championships/{id:int}")]
  public async Task<IActionResult> GetChampionship(int id)
  {
    try
    {
      return ApiOk(await _championshipService.GetByIdValidation(id));
    }
    catch (ApplicationException ex)
    {
      return ApiBadRequest("Campeonato com esse ID não existe");
    }
  }
}

