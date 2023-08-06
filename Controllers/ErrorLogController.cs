using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/error")]
[ApiController]  
[Authorize(Roles = "admin")]
public class ErrorLogController : ApiBaseController
{
    private readonly ErrorLogService _error;
    public ErrorLogController(ErrorLogService error)
    {
        _error = error;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var erros = await _error.GetAllValidationAsync();
            return ApiOk(erros);
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(Resource.GenericErrorMessage);
        }
    }
}