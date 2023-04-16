using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using System.Text.Json;
using Resource = PlayOffsApi.Resources.Championship;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/championships")]
public class ChampionshipController : ControllerBase
{
    private readonly ChampionshipService _championshipService;
    private readonly RedisService _redisService;
    public ChampionshipController(ChampionshipService championshipService, RedisService redisService)
    {
        _championshipService = championshipService;
        _redisService = redisService;
    }

    [HttpPost(Name = "create")]
    public async Task<ApiResponse<List<string>>> CreateAsync([FromBody] Championship championship)
    {
        var result = new List<string>();

        try
        {
            result = await _championshipService.CreateValidationAsync(championship);
            if (result.Any())
            {
                return new() { Succeed = false, Results = result };
            }

            result.Add(Resource.ChampionshipAdded);
            return new() { Succeed = true, Results = result };
        }
        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return new() { Succeed = false, Results = result };
        }
    }


    [HttpGet(Name = "index")]
    public async Task<ApiResponse<List<Championship>>> Index([FromQuery] string name = "")
    {
        try
        {
            List<Championship> retorno;
            await using var redisDb = await _redisService.GetDatabase();
            var cachePagina = await redisDb.GetAsync<string>(name);

            if (!string.IsNullOrEmpty(cachePagina))
                retorno = JsonSerializer.Deserialize<List<Championship>>(cachePagina.ToString());
            else
            {
                retorno = await _championshipService.GetByFilterValidationAsync(name);
                await redisDb.SetAsync(name, JsonSerializer.Serialize(retorno), TimeSpan.FromMinutes(20));
            }

            return new() { Succeed = true, Results = retorno };
        }
        catch (ApplicationException ex)
        {
            return new() { Succeed = false, Message = ex.Message };
        }
    }
}

