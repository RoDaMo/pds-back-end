using Microsoft.AspNetCore.Mvc;

namespace PlayOffsApi.API;

public abstract class ApiBaseController : ControllerBase
{
  protected OkObjectResult ApiOk<T>(T results) =>
    Ok(CustomResponse(results, true));

  protected OkObjectResult ApiOk(string message = "") =>
    Ok(CustomResponse(true, message));

  protected OkObjectResult ApiOk<T>(T results, bool succeed = true, string message = "") =>
    Ok(CustomResponse(results, succeed, message));

  protected NotFoundObjectResult ApiNotFound(string message = "") =>
    NotFound(CustomResponse(false, message));

  protected BadRequestObjectResult ApiBadRequest<T>(T results, string message = "") =>
    BadRequest(CustomResponse(results, false, message));

  protected BadRequestObjectResult ApiBadRequest<T>(string message) =>
    BadRequest(CustomResponse(false, message));

  protected UnauthorizedObjectResult ApiUnathorizedRequest(string message) =>
    Unauthorized(CustomResponse(false, message));

  private static ApiResponse<T> CustomResponse<T>(T results, bool success = true, string message = "") =>
    new()
    {
      Results = results,
      Succeed = success,
      Message = message
    };

  private static ApiResponse<string> CustomResponse(bool success = true, string message = "") =>
    new()
    {
      Succeed = success,
      Message = message
    };
}