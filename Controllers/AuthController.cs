using Microsoft.AspNetCore.Authorization;
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
	private readonly RedisService _redisService;
	public AuthController(AuthService authService, RedisService redisService)
	{
		_authService = authService;
		_redisService = redisService;
	}

	[HttpPost]
	public async Task<IActionResult> GenerateToken([FromBody]User user)
	{
		try
		{
			var redis = await _redisService.GetDatabase();
			user = await _authService.VerifyCredentials(user);
			
			if (user.Id == Guid.Empty)
				return ApiUnauthorizedRequest("Nome de usuário ou senha incorreta.");

			var jwt = _authService.GenerateJwtToken(user.Id, user.Email);

			
			var cookieOptions = new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.None,
				Expires = DateTime.UtcNow.AddHours(2)
			};
			
			if (!Request.Headers.ContainsKey("IsLocalhost"))
				cookieOptions.Domain = "playoffs.netlify.app";

			Response.Cookies.Append("playoffs-token", jwt, cookieOptions);

			if (!user.RememberMe) return ApiOk<string>("Autenticado com sucesso");
			var refreshToken = AuthService.GenerateRefreshToken(user.Id);
			await redis.SetAsync(refreshToken.Token.ToString(), refreshToken, refreshToken.ExpirationDate);
			cookieOptions.Expires = refreshToken.ExpirationDate;
			Response.Cookies.Append("playoffs-refresh-token", refreshToken.Token.ToString(), cookieOptions);

			return ApiOk<string>("Autenticado com sucesso");
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	[HttpPut]
	public async Task<IActionResult> UpdateAccesToken()
	{
		try
		{
			var oldToken = Request.Cookies["playoffs-refresh-token"];
			if (string.IsNullOrEmpty(oldToken))
				return ApiUnauthorizedRequest("Usuário não autenticado");
			
			var redis = await _redisService.GetDatabase();
			var token = await redis.GetAsync<RefreshToken>(oldToken);
			
			if (token is null || token.ExpirationDate < DateTime.Now)
				return ApiUnauthorizedRequest("Refresh token expirado");

			var user = await _authService.GetUserByIdAsync(token.UserId);
			var jwt = _authService.GenerateJwtToken(user.Id, user.Username);
				
			var cookieOptions = new CookieOptions
			{
				HttpOnly = true,
				Secure = true,
				SameSite = SameSiteMode.Strict,
				Expires = DateTime.UtcNow.AddHours(2)
			};
			Response.Cookies.Append("playoffs-token", jwt, cookieOptions);

			var refreshToken = AuthService.GenerateRefreshToken(user.Id);
			
			await redis.SetAsync(refreshToken.Token.ToString(), refreshToken, refreshToken.ExpirationDate);
			await redis.RemoveAsync(token.Token.ToString());
			
			cookieOptions.Expires = refreshToken.ExpirationDate;
			Response.Cookies.Append("playoffs-refresh-token", refreshToken.Token.ToString(), cookieOptions);
			
			return ApiOk("Token atualizado");
		}
		catch (Exception ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	} 

	[HttpDelete]
	[Authorize]
	public IActionResult LogoutUser()
	{
		Response.Cookies.Delete("playoffs-token");
		Response.Cookies.Delete("playoffs-refresh-token");
		return ApiOk<string>("Usuário deslogado com sucesso");
	}

	[HttpPost]
	[Route("/auth/register")]
	public async Task<IActionResult> RegisterUser(User user)
	{
		try
		{
			var errors = await _authService.RegisterValidationAsync(user);

			if(errors[0].Length == 36)
			{
				return ApiOk(errors[0], true, "Verifique o seu e-mail e confirme a sua conta para poder acessá-la.");
			}
			else 
			{
				return ApiOk(errors, false);
			}
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	[HttpPost]
	[Route("/auth/exists")]
	public async Task<IActionResult> UserAlreadyExists(User user)
	{
		try
		{
			return ApiOk(await _authService.UserAlreadyExists(user));
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	[Authorize]
	[HttpGet]
	public IActionResult IsLoggedIn()
	{
		return ApiOk(true);
	}

	[HttpGet]
	[Route("/auth/confirm-email")] 
	public async Task<IActionResult> ConfirmEmail(string token)
	{
		try
		{
			return ApiOk(await _authService.ConfirmEmail(token));
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	[HttpGet]
	[Route("/auth/resend-email")] 
	public async Task<IActionResult> ResendConfirmEmail(Guid id)
	{
		try
		{
			return ApiOk(await _authService.SendEmail(id));
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}
}
