using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[Route("/auth")]
[ApiController]
public class AuthController : ApiBaseController
{
	private readonly AuthService _authService;
	public AuthController(AuthService authService)
	{
		_authService = authService;
	}

	[HttpPost]
	public async Task<IActionResult> GenerateToken(User user)
	{
		try
		{
			var token = await Task.Run(() => _authService.GenerateJwtToken(user.Id, user.Email));
			return ApiOk<string>(token);
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex, "Erro");
		}
	}

	[HttpPost]
	[Route("/auth/register")]
	public async Task<IActionResult> RegisterUser(User user)
	{
		try
		{
			await _authService.RegisterUser(user);
			return ApiOk("Usuário cadastrado com sucesso");
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex, "Erro");
		}
	}
}
