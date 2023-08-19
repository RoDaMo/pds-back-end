﻿using System.Security.Claims;
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
    /// Adiciona um sub-organizador ao campeonato.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
	/// 
	///		POST /organizer
	///		{
	///			"OrganizerId": "xxxx-xxxx-xxxx-xxxx",
	///			"ChampionshipId": "51"
	///		}
	/// </remarks>
    /// <param name="newOrganizer"></param>
    /// <returns></returns>
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
    /// Remove um sub-organizador de um campeonato.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///		DELETE /organizer
    ///		{
    ///			"OrganizerId": "xxxx-xxxx-xxxx-xxxx",
    ///			"ChampionshipId": "51"
    ///		}
    /// </remarks>
    /// <param name="toBeRemoved"></param>
    /// <returns></returns>
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
    /// Obtém todos os organizadores de um campeonato. Para visualizá-los, é necessário ser o organizador principal.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
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
    /// Obtem todos os campeonatos no qual o usuário organiza
    /// </summary>
    /// <returns></returns>
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
    /// Obtem usuários para adicionar como organizadores de um campeonato.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
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
}