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

    [HttpGet]
    [Route("/statistics/{championshipId:int}/classifications")]
    public async Task<IActionResult> Index(int championshipId)
    {
        var result = new List<string>();

        try
        {
            var results = await _statisticsService.GetClassificationsValidationAsync(championshipId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }

    [HttpGet]
    [Route("/statistics/{championshipId:int}/strikers")]
    public async Task<IActionResult> GetStrikers(int championshipId)
    {
        var result = new List<string>();

        try
        {
            var results = await _statisticsService.GetStrikersValidationAsync(championshipId);
            return ApiOk(result);
        }

        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }   
    }
}