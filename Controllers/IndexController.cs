using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/")]
public class IndexController : ControllerBase
{
    // [HttpGet(Name = "index")]
    // public ApiResponse<string> TestResponse() =>
    //     new() { Succeed = true, Message = "Bem vindo à API!", Results = "Resultados" };
}

