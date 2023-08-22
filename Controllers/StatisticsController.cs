using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;
/// <summary>
///Endpoints destinados às estatísticas dos campeonatos.
/// </summary>

[ApiController]
public class StatisticsController : ApiBaseController
{
    private readonly StatisticsService _statisticsService;
    private readonly ErrorLogService _error;

    /// <inheritdoc />
    public StatisticsController(StatisticsService statisticsService, ErrorLogService error)
    {
        _statisticsService = statisticsService;
        _error = error;
    }

    /// <summary>
    /// Usado para obter as classificações dos times em um campeonato.
    /// </summary>
    /// <param name="championshipId"></param>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///		GET /statistics/{championshipId}/classifications
    ///		
    /// </remarks>
    /// <response code="200">Retorna lista de classificações</response>
    /// <response code="400">Retorna um erro indicando algum erro cometido na requisição</response>
    /// <returns>
    ///	Exemplo de retorno:
    /// {
    /// "message": "",
    /// "succeed": true,
    /// "results": [
    ///     {
    ///         "teamId": 3,
    ///         "position": 1,
    ///         "emblem": "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQpzsTC6YHwy_5CTTv27jKJYHxVMgofSsL1WA&usqp=CAU",
    ///         "name": "FC Bayern München",
    ///         "points": 3,
    ///         "amountOfMatches": 1,
    ///         "wins": 1,
    ///         "goalBalance": 2,
    ///         "proGoals": 2,
    ///         "yellowCard": 0,
    ///         "redCard": 0,
    ///         "winningSets": 0,
    ///         "losingSets": 0,
    ///         "proPoints": 0,
    ///         "pointsAgainst": 0,
    ///         "lastMatches": [
    ///             {
    ///                 "id": 4817,
    ///                 "homeName": "FC Bayern München",
    ///                 "visitorName": "alex",
    ///                 "homeEmblem": "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQpzsTC6YHwy_5CTTv27jKJYHxVMgofSsL1WA&usqp=CAU",
    ///                 "visitorEmblem": "oi",
    ///                 "homeGoals": 2,
    ///                 "visitorGoals": 0,
    ///                 "homeWinnigSets": 0,
    ///                 "visitorWinnigSets": 0,
    ///                 "isSoccer": true,
    ///                 "winnerName": null,
    ///                 "homeId": 3,
    ///                 "visitorId": 7
    ///             }
    ///         ],
    ///         "lastResults": [
    ///             {
    ///                 "tied": false,
    ///                 "won": true,
    ///                 "lose": false
    ///             }
    ///         ]
    ///     }
    ///  ]
    /// }    
    /// </returns>
    [HttpGet]
    [Route("/statistics/{championshipId:int}/classifications")]
    public async Task<IActionResult> Index(int championshipId)
    {
        var result = new List<string>();

        try
        {
            var results = await _statisticsService.GetClassificationsValidationAsync(championshipId);
            return ApiOk(results);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    /// <summary>
    /// Usado para obter os artilheiros de um campeonato.
    /// </summary>
    /// <param name="championshipId"></param>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///		GET /statistics/{championshipId:int}/strikers
    ///		
    /// </remarks>
    /// <response code="200">Retorna lista de classificações</response>
    /// <response code="400">Retorna um erro indicando algum erro cometido na requisição</response>
    /// <returns>
    ///	Exemplo de retorno:
    /// {
    ///  "message": "",
    ///  "succeed": true,
    ///  "results": [
    ///     {
    ///         "teamEmblem": "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQpzsTC6YHwy_5CTTv27jKJYHxVMgofSsL1WA&usqp=CAU",
    ///         "picture": null,
    ///         "name": "dasda",
    ///         "goals": 3
    ///     },
    ///     {
    ///         "teamEmblem": "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQpzsTC6YHwy_5CTTv27jKJYHxVMgofSsL1WA&usqp=CAU",
    ///         "picture": null,
    ///         "name": "cvc",
    ///         "goals": 2
    ///     }
    ///   ]
    /// }    
    /// </returns>
    [HttpGet]
    [Route("/statistics/{championshipId:int}/strikers")]
    public async Task<IActionResult> GetStrikers(int championshipId)
    {
        var result = new List<string>();

        try
        {
            var results = await _statisticsService.GetStrikersValidationAsync(championshipId);
            return ApiOk(results);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }
}