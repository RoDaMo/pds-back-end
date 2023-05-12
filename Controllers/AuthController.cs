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
			user = await _authService.VerifyCredentials(user);

			if (user.Id != Guid.Empty)
				return ApiOk<string>(_authService.GenerateJwtToken(user.Id, user.Username));

			return ApiUnauthorizedRequest("Nome de usuário ou senha incorreta.");
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
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
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	
}
