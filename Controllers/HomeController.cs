using Microsoft.AspNetCore.Mvc;
using pds_back_end.API;

namespace pds_back_end.Controllers;

[ApiController]
[Route("[controller]")]
public class HomeController : ControllerBase
{
    [HttpGet(Name = "Test")]
    public ApiResponse<string> TestResponse() =>
        new() { Succeed = true, Message = "Bem vindo à API!", Results = "Resultados" };
}

