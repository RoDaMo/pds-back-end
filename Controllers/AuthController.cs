using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Controllers.AuthController;

namespace PlayOffsApi.Controllers;

[Route("/auth")]
[ApiController]
public class AuthController : ApiBaseController
{
	private readonly AuthService _authService;
	private readonly RedisService _redisService;
	private readonly CookieOptions cookieOptions;
	private readonly DateTime _expires = DateTime.UtcNow.AddDays(1);

	public AuthController(AuthService authService, RedisService redisService)
	{
		_authService = authService;
		_redisService = redisService;
		cookieOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.None,
			Expires = _expires
		};
	}

	[HttpPost]
	public async Task<IActionResult> GenerateToken([FromBody] User user)
	{
		try
		{
			var redis = await _redisService.GetDatabase();
			user = await _authService.VerifyCredentials(user);

			if (user.Id == Guid.Empty)
				return ApiUnauthorizedRequest(Resource.GenerateTokenNomeInvalido);
			
			if (!user.ConfirmEmail)
				return ApiUnauthorizedRequest(Resource.GenerateTokenConfirmeEmail);

			var jwt = _authService.GenerateJwtToken(user.Id, user.Email, _expires);

			if (!Request.Headers.ContainsKey("IsLocalhost"))
				cookieOptions.Domain = "playoffs.app.br";

			Response.Cookies.Append("playoffs-token", jwt, cookieOptions);

			var expirationDate = user.RememberMe ? DateTime.UtcNow.AddDays(14) : DateTime.UtcNow.AddDays(1);

			var refreshToken = AuthService.GenerateRefreshToken(user.Id, expirationDate);
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
				return ApiUnauthorizedRequest(Resource.UpdateAccesTokenNaoAutenticado);

			var redis = await _redisService.GetDatabase();
			var token = await redis.GetAsync<RefreshToken>(oldToken);

			if (token is null || token.ExpirationDate < DateTime.Now)
				return ApiUnauthorizedRequest(Resource.UpdateAccesTokenRefreshTokenExpirado);

			var user = await _authService.GetUserByIdAsync(token.UserId);
			var jwt = _authService.GenerateJwtToken(user.Id, user.Username, _expires);
			
			if (!Request.Headers.ContainsKey("IsLocalhost"))
				cookieOptions.Domain = "playoffs.app.br";
			
			Response.Cookies.Append("playoffs-token", jwt, cookieOptions);

			return ApiOk(Resource.UpdateAccesTokenTokenAtualizado);
		}
		catch (Exception)
		{
			return ApiBadRequest(Resource.UpdateAccesTokenErroAutenticando);
		}
	}

	[HttpDelete]
	[Authorize]
	public IActionResult LogoutUser()
	{
		Response.Cookies.Delete("playoffs-token", cookieOptions);
		Response.Cookies.Delete("playoffs-refresh-token", cookieOptions);
		return ApiOk<string>(Resource.LogoutUserDeslogadoSucesso);
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
				return ApiOk(errors[0], true, Resource.RegisterUserCadastroRealizadoSucesso);
			}

			return ApiBadRequest(errors);
			
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, Resource.RegisterUserErro);
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
			return ApiBadRequest(ex.Message, Resource.UserAlreadyExistsErro);
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
			return ApiBadRequest(ex.Message, Resource.ConfirmEmailErro);
		}
	}

	[HttpGet]
	[Route("/auth/resend-confirm-email")] 
	public async Task<IActionResult> ResendConfirmEmail(Guid id)
	{
		try
		{
			await _authService.SendEmailToConfirmAccount(id);
			return ApiOk(Resource.ResendConfirmEmailEnviado);
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	[HttpPost]
	[Route("/auth/forgot-password")] 
	public async Task<IActionResult> ForgotPassword(User user)
	{
		try
		{
			var result = await _authService.ForgotPassword(user);

			if(result[0].Length == 36)
			{
				return ApiOk(result[0], true, Resource.ForgotPasswordRedefinicaoSenha);
			}
			
			return ApiBadRequest(result);
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, Resource.ForgotPasswordErro);
		}
	}

	[Authorize]
	[HttpGet]
	[Route("/auth/user")]
	public async Task<IActionResult> GetCurrentUser()
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			var user = await _authService.GetUserByIdAsync(userId);
			return ApiOk(new
			{
				email = user.Email,
				userName = user.Username,
				bio = user.Bio,
				picture = user.Picture,
				profileImg = user.Picture,
				name = user.Name,
				id = user.Id,
				championshipId = user.ChampionshipId,
				teamManagementId = user.TeamManagementId
			});
		}
		catch (Exception ex)
		{
			return ApiBadRequest(ex.Message);
		}
	}

	[HttpGet]
	[Route("/auth/resend-forgot-password")] 
	public async Task<IActionResult> ResendForgotPassword(Guid id)
	{
		try
		{
			await _authService.SendEmailToResetPassword(id);
			return ApiOk(Resource.ResendForgotPasswordEmailDeConfirmacaoReenviado);
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, "Erro");
		}
	}

	[HttpGet]
	[Route("/auth/reset-password")] 
	public IActionResult ResetPassword(string token)
	{
		try
		{
			return ApiOk(_authService.ConfirmResetPassword(token));
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, Resource.ResetPasswordErro);
		}
	}

	[HttpPost]
	[Route("/auth/reset-password")] 
	public async Task<IActionResult> ResetPassword(User user)
	{
		try
		{
			var result = await _authService.ResetPassword(user);
			return result.Any() ? ApiBadRequest(result) : ApiOk(result);
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(ex.Message, Resource.ResetPasswordErro);
		}
	}

	[Authorize]
	[HttpGet]
	[Route("/auth/cpf")]
	public async Task<IActionResult> CurrentUserHasCpf()
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			var hasCpf = await _authService.UserHasCpfValidationAsync(userId);
			return ApiOk(hasCpf);
		}
		catch (Exception ex)
		{
			return ApiBadRequest(ex.Message);
		}
	}

	[Authorize]
	[HttpPost]
	[Route("/auth/cpf")]
	public async Task<IActionResult> AddCpf([FromBody] string cpf)
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			var resultados = await _authService.AddCpfUserValidationAsync(userId, cpf);
			
			if (resultados.Any())
				return ApiBadRequest(resultados);
			
			return ApiOk(Resource.AddCpfCPFVinculadoComSucesso);
		}
		catch (Exception ex)
		{
			return ApiBadRequest(ex.Message);
		}
	}

	[HttpGet]
	[Route("/auth/{id:guid}")]
	public async Task<IActionResult> GetById(Guid id)
	{
		try
		{
			return ApiOk(await _authService.GetUserByIdAsync(id));
		}
		catch (Exception)
		{
			return ApiBadRequest(Resource.GetByIdUsuarioNaoExiste);
		}
	}

	[HttpDelete]
	[Authorize]
	[Route("/auth/user")]
	public async Task<IActionResult> Delete()
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			await _authService.DeleteCurrentUserValidation(userId);
			Response.Cookies.Delete("playoffs-token");
			Response.Cookies.Delete("playoffs-refresh-token");
			
			return ApiOk(Resource.DeleteUsuarioExcluidoComSucesso);
		}
		catch (Exception)
		{
			return ApiBadRequest(Resource.DeleteHouveErroExcluirUsuario);
		}
	}
}
