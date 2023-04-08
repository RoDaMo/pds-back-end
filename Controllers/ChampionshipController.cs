using Microsoft.AspNetCore.Mvc;
using pds_back_end.API;
using pds_back_end.Models;
using pds_back_end.Services;

namespace pds_back_end.Controllers;

[ApiController]
[Route("/championship")]
public class ChampionshipController : ControllerBase
{
    private readonly ElasticService _elastic;
    private readonly ChampionshipService _championshipService;
    public ChampionshipController(ChampionshipService championshipService, ElasticService elastic)
    {
        _elastic = elastic;
        _championshipService = championshipService;
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
            return new() { Succeed = true, Results = await _championshipService.GetByFilter(name)};            
        }
        catch (ApplicationException ex)
        {
            return new() { Succeed = false, Message = ex.Message };
        }
    } 
}

