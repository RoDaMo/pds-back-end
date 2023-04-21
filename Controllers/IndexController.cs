using Microsoft.AspNetCore.Mvc;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/")]
public class IndexController : ControllerBase
{
	// [HttpGet(Name = "index")]
	// public ApiResponse<string> TestResponse() =>
	//     new() { Succeed = true, Message = "Bem vindo à API!", Results = "Resultados" };
}

