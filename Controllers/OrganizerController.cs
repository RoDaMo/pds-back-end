using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Generic = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoints destinados à manuntenção de organizadores de campeonatos
/// </summary>
[ApiController]
[Authorize]
[Route("/organizer")]
public class OrganizerController  : ApiBaseController
{
    private readonly OrganizerService _organizerService;
    private readonly AuthService _authService;
    private readonly ChampionshipService _championshipService;
    private readonly ErrorLogService _error;
    /// <inheritdoc />
    public OrganizerController(OrganizerService organizerService, AuthService authService, ChampionshipService championshipService, ErrorLogService errorLogService)
    {
        _organizerService = organizerService;
        _authService = authService;
        _championshipService = championshipService;
        _error = errorLogService;
    }


    /// <summary>
	/// Usado para adicionar novo suborganizador.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /organizer
    ///		{
    ///         "organizerId": "d7868182-c7ff-4fcc-91dc-c797f4bfd09e",
    ///         "championshipId": 66
    ///     }
	///		
	/// </remarks>
	/// <response code="200">Adiciona um novo suborganizador ao campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpPost]
    public async Task<IActionResult> AddOrganizerToChampionship(Organizer newOrganizer)
    {
        try
        {
            var championshipExists = await _championshipService.GetByIdValidation(newOrganizer.ChampionshipId) is null;
            if (championshipExists)
                return ApiBadRequest("Este campeonato não existe.");
            
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var isUser = await _organizerService.IsUserAnOrganizerValidation(new Organizer { ChampionshipId = newOrganizer.ChampionshipId, OrganizerId = userId });
            if (isUser is null || !isUser.MainOrganizer)
                return ApiBadRequest("Você não possui permissão para adicionar este usuário como organizador.");

            var userExists = await _authService.GetUserByIdAsync(newOrganizer.OrganizerId) is null;
            if (userExists)
                return ApiBadRequest("Este usuário não existe.");

            var alreadyOrganizer = await _organizerService.IsUserAnOrganizerValidation(newOrganizer.OrganizerId) is not null;
            if (alreadyOrganizer)
                return ApiBadRequest("Usuário já é organizador deste campeonato.");
            
            newOrganizer.OrganizerId = newOrganizer.OrganizerId;
            newOrganizer.MainOrganizer = false;
            await _organizerService.InsertValidation(newOrganizer);
            return ApiOk("Usuário adicionado como organizador deste campeonato.");
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Generic.GenericErrorMessage);
        }

    }

    /// <summary>
	/// Usado para remover suborganizador.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /organizer
    ///		{
    ///         "organizerId": "d7868182-c7ff-4fcc-91dc-c797f4bfd09e",
    ///         "championshipId": 66
    ///     }
	///		
	/// </remarks>
	/// <response code="200">Remove um suborganizador do campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpDelete]
    public async Task<IActionResult> RemoveOrganizerFromChampionship(Organizer toBeRemoved)
    {
        try
        {
            var championshipExists = await _championshipService.GetByIdValidation(toBeRemoved.ChampionshipId) is null;
            if (championshipExists)
                return ApiBadRequest("Este campeonato não existe.");
            
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var isUser = await _organizerService.IsUserAnOrganizerValidation(new Organizer { ChampionshipId = toBeRemoved.ChampionshipId, OrganizerId = userId });
            if (isUser is null || !isUser.MainOrganizer)
                return ApiBadRequest("Você não possui permissão para remover este usuário como organizador.");

            var userExists = await _authService.GetUserByIdAsync(toBeRemoved.OrganizerId) is null;
            if (userExists)
                return ApiBadRequest("Este usuário não existe.");

            await _organizerService.DeleteValidation(toBeRemoved);
            return ApiOk("Sub-organizador removido do campeonato.");
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Generic.GenericErrorMessage);
        }    
    }

    /// <summary>
	/// Usado para obter todos os suborganizadores.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /organizer/championship/{id}
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os suborganizadores do campeonato.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpGet]
    [Route("/organizer/championship/{id:int}")]
    public async Task<IActionResult> GetAllOrganizersFromChampionship(int id)
    {
        try
        {
            var championshipExists = await _championshipService.GetByIdValidation(id) is null;
            if (championshipExists)
                return ApiBadRequest("Este campeonato não existe.");
            
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var isUser = await _organizerService.IsUserAnOrganizerValidation(new Organizer { ChampionshipId = id, OrganizerId = userId });
            if (isUser is null || !isUser.MainOrganizer)
                return ApiBadRequest("Você não possui permissão para visualizar os organizadores desse campeonato.");

            return ApiOk(await _organizerService.GetAllOrganizersValidation(id));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Generic.GenericErrorMessage);
        }
    }
    
    /// <summary>
	/// Usado para obter todos os campeonatos os quais o usuário administra.
	/// </summary>
    /// <param name="username"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /organizer/championship
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os campeonatos administrados pelo usuário.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpGet]
    [Route("/organizer/championship")]
    public async Task<IActionResult> GetAllChampionshipsFromOrganizer()
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var isUser = await _organizerService.IsUserAnOrganizerValidation(userId);
            if (isUser is null)
                return ApiBadRequest("Você não possui nenhum campeonato no qual você é organizador.");

            return ApiOk(await _organizerService.GetAllChampionshipsByOrganizerValidation(userId));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Generic.GenericErrorMessage);
        }
    }
    
    /// <summary>
	/// Usado para obter usuários para adicionar como suborganizadores.
	/// </summary>
    /// <param name="username"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /organizer?username={username}
	///		
	/// </remarks>
	/// <response code="200">Obtém todos os usuários conforme o username.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetByQuery(string username)
    {
        try
        {
            var users = await _authService.GetUsersByUsernameValidation(username, true);
            return ApiOk(users.Select(s => new { s.Name, s.Username, s.Picture, s.Id }));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Generic.GenericErrorMessage);
        }
    }

    ///  <summary>
    ///  Usado para obter todos os campeonatos os quais o usuário com o ID fornecido administra.
    ///  </summary>
    ///  <param name="id"></param>
    ///  <remarks>
    ///  Exemplo de requisição:
    ///  
    /// 		GET /organizer/championship/75ff2f9e-ae41-487c-8316-791213de27fd
    /// 		
    ///  </remarks>
    ///  <response code="200">Obtém todos os campeonatos administrados pelo usuário.</response>
    ///  <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
    ///  <returns>
    ///  </returns>
    [HttpGet]
    [Route("/organizer/championship/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllChampionshipsFromOrganizer(Guid id)
    {
	    try
	    {
		    var isUser = await _organizerService.IsUserAnOrganizerValidation(id);
		    if (isUser is null)
			    return ApiBadRequest("Usuário com o ID fornecido não possui nenhum campeonato organizado");

		    return ApiOk(await _organizerService.GetAllChampionshipsByOrganizerValidation(id));
	    }
	    catch (ApplicationException ex)
	    {
		    await _error.HandleExceptionValidationAsync(HttpContext, ex);
		    return ApiBadRequest(Generic.GenericErrorMessage);
	    }
    }
}