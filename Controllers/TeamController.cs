using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.DTO;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Controllers.TeamController;

namespace PlayOffsApi.Controllers;
/// <summary>
///Endpoints destinados à manuntenção dos times.
/// </summary>

[Authorize]
[ApiController]
[Route("/teams")]
public class TeamController : ApiBaseController
{
    private readonly TeamService _teamService;
    private readonly ChampionshipService _championshipService;
    private readonly ErrorLogService _error;
    private readonly ChampionshipActivityLogService _activityLogService;
    /// <inheritdoc />
    public TeamController(TeamService teamService, ChampionshipService championshipService, ErrorLogService error, ChampionshipActivityLogService activityLogService)
    {
        _teamService = teamService;
        _championshipService = championshipService;
        _error = error;
        _activityLogService = activityLogService;
    }

    /// <summary>
	/// Usado para cadastrar times.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /teams
	///		{
    ///         "emblem": "",
    ///         "uniformHome": "",
    ///         "uniformAway": "",
    ///         "sportsId": "1",
    ///         "name": "FC Borussia München De Vôlei 2"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Time é cadastrado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAsync([FromBody] TeamDTO teamDto)
    {
        var result = new List<string>();

        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            result = await _teamService.CreateValidationAsync(teamDto, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(result);
        }
    }

    /// <summary>
	/// Usado para obter times por query.
	/// </summary>
    /// <param name="query"></param>
    /// <param name="sport"></param>
    /// <param name="championshipId"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET teams?query=""&sport=""&championshipId=""
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os times conforme a query.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery]string query, Sports sport, int championshipId)
    {
        try
        {
            var result = string.IsNullOrEmpty(query) ? await _teamService.GetAllValidationAsync(sport) : await _teamService.SearchTeamsValidation(query, sport, championshipId);
            return ApiOk(result);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
    
    /// <summary>
	/// Usado para obter times por id.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET teams/{id}
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os times conforme o id.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        try
        {
            var result = await _teamService.GetByIdValidationAsync(id);

            return result is null ? ApiBadRequest(Resource.ShowTimeNaoExistente) : ApiOk(new
            {
                id = result.Id,
                emblem = result.Emblem,
                uniformHome = result.UniformHome,
                uniformAway = result.UniformAway,
                deleted = result.Deleted,
                sportsId = result.SportsId,
                name = result.Name,
                technician = new {
                    name = result.Technician.Name,
                    picture = result.Technician.Picture
                }
            });
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
	/// Usado para vincular um time a um campeonato.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /teams/championship
	///		{
    ///         "teamId": 5,
    ///         "championshipId": 47
	///		}
	///		
	/// </remarks>
	/// <response code="200">Time será vinculado ao campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpPost]
    [Authorize]
    [Route("/teams/championship")]
    public async Task<IActionResult> AddTeamToChampionship([FromBody]ChampionshipTeamsDto championshipTeamsDto)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(championshipTeamsDto.ChampionshipId);
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (championship.OrganizerId != userId)
                return ApiBadRequest(Resource.AddTeamToChampionshipNoPermission);

            await _teamService.AddTeamToChampionshipValidation(championshipTeamsDto.TeamId, championshipTeamsDto.ChampionshipId);

            await _activityLogService.InsertValidation(new()
            {
                DateOfActivity = DateTime.UtcNow,
                ChampionshipId = championship.Id,
                TypeOfActivity = TypeOfActivity.AddedTeam,
                OrganizerId = userId
            });
            
            return ApiOk(Resource.AddTeamToChampionshipVinculado);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
	/// Usado para remover time de um campeonato.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE teams/championship
    ///		{
    ///         "championshipId": 1168,
    ///         "teamId": 44
    ///     }
	///		
	/// </remarks>
	/// <response code="200">Remove um time do campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpDelete]
    [Authorize]
    [Route("/teams/championship")]
    public async Task<IActionResult> RemoveTeamFromChampionship([FromBody]ChampionshipTeamsDto championshipTeamsDto)
    {
        try
        {
            var championship = await _championshipService.GetByIdValidation(championshipTeamsDto.ChampionshipId);
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (championship.OrganizerId != userId)
                return ApiBadRequest(Resource.RemoveTeamFromChampionshipNoPermissionToDelete);

            await _teamService.RemoveTeamFromChampionshipValidation(championshipTeamsDto.TeamId, championshipTeamsDto.ChampionshipId);
            
            await _activityLogService.InsertValidation(new()
            {
                DateOfActivity = DateTime.UtcNow,
                ChampionshipId = championship.Id,
                TypeOfActivity = TypeOfActivity.RemovedTeam,
                OrganizerId = userId
            });
            
            return ApiOk(Resource.RemoveTeamFromChampionshipUnlinked);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
	/// Usado para obter os campeonatos no qual o time participa.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET teams/championship/{id}
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os campeonatos que o time participa.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpGet]
    [Route("/teams/championship/{id:int}")]
    public async Task<IActionResult> GetTeamChampionships(int id)
    {
        try
        {
            return ApiOk(await _teamService.GetChampionshipsOfTeamValidation(id));
        }
        catch (ApplicationException ex)  
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
	/// Usado para atualizar times.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		PUT /teams
	///		{
    ///		    "id": 5,
    ///         "emblem": "",
    ///         "uniformHome": "",
    ///         "uniformAway": "",
    ///         "sportsId": "1",
    ///         "name": "FC Borussia München De Vôlei 2"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Time é atualizado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpPut]
    [Authorize]
    [Route("/teams")]
    public async Task<IActionResult> UpdateTeam(TeamDTO team)
    {
        var result = new List<string>();
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            result = await _teamService.UpdateTeamValidation(team, userId);
            return result.Any() ? ApiBadRequest(result) : ApiOk(Resource.TeamSuccessfullyUpdated);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            result.Add(ex.Message);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpDelete]
    [Route("/teams/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteTeam(int id)
    {
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            await _teamService.DeleteTeamValidation(id, userId);
            return ApiOk(Resource.TeamDeleted);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    [HttpGet]
    [Route("/teams/{id:int}/players")]
    public async Task<IActionResult> GetAllPlayersOfTeam(int id)
    {
        try
        {
            var players = await _teamService.GetPlayersOfTeamValidation(id); 
            return ApiOk(players.Select(m => new { id = m.Id, name = m.Name, artisticName = m.ArtisticName, number = m.Number, teamsId = m.PlayerTeamId, playerPosition = m.PlayerPosition, isCaptain = m.IsCaptain, picture = m.Picture }));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}
