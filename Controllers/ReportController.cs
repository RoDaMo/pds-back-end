using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoint dedicado à envio de denúncias de usuários, assim como o gerenciamento das mesmas.
/// </summary>
[ApiController]
[Authorize]
[Route("/reports")]
public class ReportController : ApiBaseController
{
    private readonly ReportService _reportService;
    private readonly ErrorLogService _error;
    public ReportController(ReportService reportService, ErrorLogService error)
    {
        _reportService = reportService;
        _error = error;
    }
    
    /// <summary>
    /// Criar uma denúncia
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///		POST /reports
    ///		{
    ///        "AuthorId": "e0b595dc-bcc3-4d08-ab3b-68b194a45974",
    ///        "ReportedType": 0,
    ///        "ReportedUserId": "e0b595dc-bcc3-4d08-ab3b-68b194a45974",
    ///        "ReportedTeamId": 0,
    ///        "ReportedChampionshipId": 5,
    ///        "Description": "Teste"
    ///        "Violation": 1
    ///     }
    ///		
    /// </remarks>
    /// <param name="report"></param>
    /// <response code="200">Retorna se o usuário existe ou não.</response>
    /// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
    /// <returns>
    ///	Exemplo de retorno:
    ///
    ///		{
    ///			"message": "",
    ///			"succeed": true,
    ///			"results": true
    ///		}
    ///		
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> CreateReport(Report report)
    {
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            report.AuthorId = userId;
            await _reportService.CreateReportValidation(report);
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Obter por tipo
    /// </summary>
    /// <param name="type"></param>
    /// <param name="completed"></param>
    /// <param name="typeOfViolation"></param>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///     GET /reports?type=0&amp;completed=false&amp;typeOfActivity=0
    /// 
    /// </remarks>
    /// <returns>\
    /// Exemplo de retorno:
    /// 
    ///     {
    ///         "message": "",
    ///         "succeed": true,
    ///         "results": [
    ///         {
    ///             "id": 3,
    ///             "authorId": "140804b5-e3b5-495e-92e8-8f6c534972ee",
    ///             "completed": false,
    ///             "reportType": 0,
    ///             "reportedUserId": null,
    ///             "reportedTeamId": null,
    ///             "reportedChampionshipId": 5,
    ///             "description": "Teste"
    ///         }
    ///         ]
    ///     }
    /// </returns>
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetReportsByType(ReportType type, bool completed, TypeOfViolation typeOfViolation)
    {
        try
        {
            return ApiOk(await _reportService.GetAllByTypeValidation(type, completed, typeOfViolation));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Obter por ID
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///		GET /reports/3
    /// 
    /// </remarks>
    /// <param name="id"></param>
    /// <returns>
    /// Exemplo de retorno:
    /// 
    ///     {
    ///         "message": "",
    ///         "succeed": true,
    ///         "results": {
    ///             "id": 3,
    ///             "authorId": "140804b5-e3b5-495e-92e8-8f6c534972ee",
    ///             "completed": false,
    ///             "reportType": 0,
    ///             "reportedUserId": null,
    ///             "reportedTeamId": null,
    ///             "reportedChampionshipId": 5,
    ///             "description": "Teste"
    ///         }
    ///     }
    /// </returns>
    [HttpGet]
    [Authorize(Roles = "admin")]
    [Route("/reports/{id:int}")]
    public async Task<IActionResult> GetReportById(int id)
    {
        try
        {
            return ApiOk(await _reportService.GetByIdValidation(id));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
    
    /// <summary>
    /// Obter por ID de usuário
    /// </summary>
    /// <param name="id"></param>
    /// <remarks>
    /// Exemplo de requisição:
    ///
    ///     GET /reports/user/140804b5-e3b5-495e-92e8-8f6c534972ee
    /// 
    /// </remarks>
    /// <returns>\
    /// Exemplo de retorno:
    /// 
    ///     {
    ///         "message": "",
    ///         "succeed": true,
    ///         "results": [
    ///         {
    ///             "id": 3,
    ///             "authorId": "140804b5-e3b5-495e-92e8-8f6c534972ee",
    ///             "completed": false,
    ///             "reportType": 0,
    ///             "reportedUserId": null,
    ///             "reportedTeamId": null,
    ///             "reportedChampionshipId": 5,
    ///             "description": "Teste"
    ///         }
    ///         ]
    ///     }
    /// </returns>
    [HttpGet]
    [Authorize(Roles = "admin")]
    [Route("/reports/user/{id:guid}")]
    public async Task<IActionResult> GetReportByUserId(Guid id)
    {
        try
        {
            return ApiOk(await _reportService.GetReportsFromUserValidation(id));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
    
    /// <summary>
    /// Atualizar status da denúncia
    /// </summary>
    /// <param name="report"></param>
    /// <remarks>
    /// Exemplo de requisição:
    ///
    ///     PUT /reports
    ///     {
    ///         "Id": 3,
    ///         "Completed": true
    ///     }
    /// 
    /// </remarks>
    /// <returns>\
    /// Exemplo de retorno:
    /// 
    ///     {
    ///         "message": "",
    ///         "succeed": true,
    ///         "results": [
    ///         {
    ///             "id": 3,
    ///             "authorId": "140804b5-e3b5-495e-92e8-8f6c534972ee",
    ///             "completed": false,
    ///             "reportType": 0,
    ///             "reportedUserId": null,
    ///             "reportedTeamId": null,
    ///             "reportedChampionshipId": 5,
    ///             "description": "Teste"
    ///         }
    ///         ]
    ///     }
    /// </returns>
    [HttpPut]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateStatusReport(Report report)
    {
        try
        {
            await _reportService.SetReportAsCompletedValidation(report.Id, report.Completed);
            return ApiOk();
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Verifica se o usuário logado atualmente já possui uma denúncia naquela entidade.
    /// </summary>
    /// <remarks>
    /// Exemplo de requisição:
    ///
    ///     GET /reports/verify?id=3
    /// 
    /// </remarks>
    /// <param name="idUser"></param>
    /// <param name="id"></param>
    /// <returns>
    ///     {
    ///         "message": "",
    ///         "succeed": true,
    ///         "results": true
    ///     }
    /// </returns>
    [HttpGet]
    [Authorize]
    [Route("/reports/verify")]
    public async Task<IActionResult> VerifyIfUserHasReportedEntity(Guid idUser = new(), int id = 0)
    {
        try
        {
            var userId =  Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            return ApiOk(await _reportService.VerifyReportedEntity(idUser, id, userId));
        }
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}
