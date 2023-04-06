using Microsoft.AspNetCore.Mvc;
using pds_back_end.API;
using pds_back_end.Models;
using pds_back_end.Services;

namespace pds_back_end.Controllers;

[ApiController]
[Route("/Championship")]
public class ChampionshipController : ControllerBase
{
    private readonly ChampionshipService _championshipService;
    public ChampionshipController(ChampionshipService championshipService)
    {
        _championshipService = championshipService;
    }

    [HttpPost(Name = "create")]
    public async Task<ApiResponse<List<string>>> CreateAsync([FromBody]Championship championship) 
    {
        var result = new List<string>();
        
        result = await _championshipService.CreateValidationAsync(championship);
        if(result.Any())
        {
            return new() { Succeed = false, Results = result };
        }

        result.Add("Campeonato cadastrado");

        return new() { Succeed = true, Results = result };
    }
       
}

