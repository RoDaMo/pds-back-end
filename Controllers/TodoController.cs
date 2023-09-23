using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;
using GenericError = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoints dedicados à pendencias do campeonato
/// </summary>
[Route("/todo")]
[ApiController]
public class TodoController : ApiBaseController
{
    private readonly TodoService _todoService;    
    private readonly ErrorLogService _error;
    public TodoController(TodoService todoService, ErrorLogService error)
    {
        _todoService = todoService;
        _error = error;
    }
    
    /// <summary>
    /// Obtém todas as pendências do campeonato
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet]
    [Authorize]
    [Route("/todo/{id:int}")]
    public async Task<IActionResult> GetTodoOfChampionship(int id)
    {
        try
        {
            return ApiOk(await _todoService.GetTodoListOfChampionship(id));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
        }
    }
}