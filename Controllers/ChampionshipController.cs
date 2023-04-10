using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using pds_back_end.API;
using pds_back_end.Models;
using pds_back_end.Services;

namespace pds_back_end.Controllers;

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
    public async Task<ApiResponse<List<string>>> CreateAsync([FromBody]Championship championship) 
    {
        var result = new List<string>();

        try
        {
            result = await _championshipService.CreateValidationAsync(championship);
            if(result.Any())
            {
                return new() { Succeed = false, Results = result };
            }

            result.Add("Campeonato cadastrado");
            return new() { Succeed = true, Results = result };
        }
        catch (ApplicationException ex)
        {
            result.Add(ex.Message);
            return new() { Succeed = false, Results = result };
        }
    }


    [HttpGet(Name = "index")]
    public async Task<ApiResponse<List<Championship>>> Index([FromQuery]string name = "") 
    {
        try
        {
            List<Championship> retorno;
            var redisDb = _redisService.Database;
            
            if (redisDb.KeyExists(name)) 
            {
                var algo = await redisDb.StringGetAsync(name);
                retorno = JsonSerializer.Deserialize<List<Championship>>(algo.ToString());
            }
            else 
            {
                retorno = await _championshipService.GetByFilter(name);
                await redisDb.StringSetAsync(name, JsonSerializer.Serialize(retorno), TimeSpan.FromMinutes(20));
            }

            return new() { Succeed = true, Results = retorno };            
        }
        catch (ApplicationException ex)
        {
            return new() { Succeed = false, Message = ex.Message };
        }
    } 
}

