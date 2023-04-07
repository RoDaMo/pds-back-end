using Microsoft.AspNetCore.Mvc;
using pds_back_end.API;
using pds_back_end.Services;

namespace pds_back_end.Controllers;

[ApiController]
[Route("/")]
public class IndexController : ControllerBase
{
    // [HttpGet(Name = "index")]
    // public ApiResponse<string> TestResponse() =>
    //     new() { Succeed = true, Message = "Bem vindo à API!", Results = "Resultados" };
}

