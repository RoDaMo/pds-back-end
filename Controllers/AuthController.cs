﻿using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Controllers.AuthController;
using GenericError = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Controllers;

/// <summary>
/// Endpoints destinados ao CRUD de usuários, e o sistema de autenticação.
/// </summary>
[Route("/auth")]
[ApiController]
[Produces("application/json")]
public class AuthController : ApiBaseController
{
	private readonly AuthService _authService;
	private readonly RedisService _redisService;
	private readonly CookieOptions _cookieOptions;
	private readonly CaptchaService _captcha;
	private readonly DateTime _expires = DateTime.UtcNow.AddDays(1);
	private readonly ErrorLogService _error;
	private readonly OrganizerService _organizerService;
	private readonly WoService _woService;

	/// <inheritdoc />
	public AuthController(AuthService authService, RedisService redisService, ErrorLogService error, CaptchaService captcha, OrganizerService organizerService, WoService woService)
	{
		_authService = authService;
		_redisService = redisService;
		_error = error;
		_captcha = captcha;
		_organizerService = organizerService;
		_cookieOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.None,
			Expires = _expires
		};
		_woService = woService;
	}

	/// <summary>
	/// Usado para gerar tokens de acesso para usuários.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /auth
	///		{
	///			"Username": "Kaique",
	///			"Password": "Ab123",
	///			"RememberMe": true
	///		}
	///		
	/// </remarks>
	/// <response code="200">Retorna o token recém-criado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": "Autenticado com sucesso"
	///		}
	///		
	/// </returns>
	[HttpPost]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public async Task<IActionResult> GenerateToken([FromBody] User user)
	{
		try
		{
			if (!await _captcha.VerifyValidityCaptcha(user.CaptchaToken)) 
				throw new ApplicationException(Resource.InvalidCaptcha);
			
			await using var redis = await _redisService.GetDatabase();
			user = await _authService.VerifyCredentials(user);

			if (user.Id == Guid.Empty)
				return ApiUnauthorizedRequest(Resource.GenerateTokenNomeInvalido);
			
			if (!user.ConfirmEmail)
				return ApiUnauthorizedRequest(Resource.GenerateTokenConfirmeEmail);

			var jwt = _authService.GenerateJwtToken(user.Id, user.Email, _expires, user.Role);

			if (!Request.Headers.ContainsKey("IsLocalhost"))
				_cookieOptions.Domain = "playoffs.app.br";

			Response.Cookies.Append("playoffs-token", jwt, _cookieOptions);

			var expirationDate = user.RememberMe ? DateTime.UtcNow.AddDays(14) : DateTime.UtcNow.AddDays(1);

			var refreshToken = AuthService.GenerateRefreshToken(user.Id, expirationDate);
			await redis.SetAsync(refreshToken.Token.ToString(), refreshToken, refreshToken.ExpirationDate);
			_cookieOptions.Expires = refreshToken.ExpirationDate;
			Response.Cookies.Append("playoffs-refresh-token", refreshToken.Token.ToString(), _cookieOptions);

			return ApiOk<string>("Autenticado com sucesso");
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
		}
	}

	/// <summary>
	/// Usado para atualizar tokens de acesso para usuários.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		Put /auth
	///		
	/// </remarks>
	/// <response code="200">O token é atualizado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "Token atualizado",
  	///			"succeed": true,
  	///			"results": ""
	///		}
	///		
	/// </returns>
	[HttpPut]
	public async Task<IActionResult> UpdateAccesToken()
	{
		try
		{
			var oldToken = Request.Cookies["playoffs-refresh-token"];
			if (string.IsNullOrEmpty(oldToken))
				return ApiUnauthorizedRequest(Resource.UpdateAccesTokenNaoAutenticado);

			await using var redis = await _redisService.GetDatabase();
			var token = await redis.GetAsync<RefreshToken>(oldToken);

			if (token is null || token.ExpirationDate < DateTime.Now)
				return ApiUnauthorizedRequest(Resource.UpdateAccesTokenRefreshTokenExpirado);

			var user = await _authService.GetUserByIdAsync(token.UserId);
			var jwt = _authService.GenerateJwtToken(user.Id, user.Username, _expires);
			
			if (!Request.Headers.ContainsKey("IsLocalhost"))
				_cookieOptions.Domain = "playoffs.app.br";
			
			Response.Cookies.Append("playoffs-token", jwt, _cookieOptions);

			return ApiOk(Resource.UpdateAccesTokenTokenAtualizado);
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(Resource.UpdateAccesTokenErroAutenticando);
		}
	}

	/// <summary>
	/// Usado para encerrar a sessão do usuário no sistema.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /auth
	///		
	/// </remarks>
	/// <response code="200">A sessão do usuário é encerrada no sistema.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "",
	///			"succeed": true,
	///			"results": "Usuário deslogado com sucesso"
	///		}
	///		
	/// </returns>
	[HttpDelete]
	[Authorize]
	public IActionResult LogoutUser()
	{
		Response.Cookies.Delete("playoffs-token", _cookieOptions);
		Response.Cookies.Delete("playoffs-refresh-token", _cookieOptions);
		return ApiOk<string>(Resource.LogoutUserDeslogadoSucesso);
	}

	/// <summary>
	/// Usado para cadastrar usuário.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /auth/register
	///		{
	///			"Name": "Usuário Admin 10",
	///			"Email": "jerobe6695@meogl.com",
	///			"Password": "Ab123",
	///			"Username": "UsuarioAdmin10",
	///			"Birthday": "2004-06-14"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Registra o usuário.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	///	Exemplo de retorno:
	///
	///		{
	///			"message": "Usuário cadastrado com sucesso",
	///			"succeed": true,
	///			"results": ""
	///		}
	///		
	/// </returns>
	[HttpPost]
	[Route("/auth/register")]
	public async Task<IActionResult> RegisterUser(User user, [FromHeader]string superSecretPassword = "")
	{
		try
		{
			if (!await _captcha.VerifyValidityCaptcha(user.CaptchaToken))
				throw new ApplicationException(Resource.InvalidCaptcha);
			
			user.Role = superSecretPassword == Environment.GetEnvironmentVariable("SUPER_SECRET_PASSWORD") ? "admin" : "user";
			var errors = await _authService.RegisterValidationAsync(user);
			
			if(errors[0].Length == 36)
				return ApiOk(errors[0], true, Resource.RegisterUserCadastroRealizadoSucesso);
			
			return ApiBadRequest(errors);
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, Resource.RegisterUserErro);
		}
	}


	/// <summary>
	/// Usado para verificar se usuário existe.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /auth/exists
	///		{
	///			"Username": "UsuarioAdmin10"
	///		}
	///		
	/// </remarks>
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
	[Route("/auth/exists")]
	public async Task<IActionResult> UserAlreadyExists(User user)
	{
		try
		{
			return ApiOk(await _authService.UserAlreadyExists(user));
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, Resource.UserAlreadyExistsErro);
		}
	}


	[Authorize]
	[HttpGet]
	[Obsolete("Useless")]
	[ApiExplorerSettings(IgnoreApi = true)]
	public IActionResult IsLoggedIn()
	{
		return ApiOk(true);
	}

    /// <summary>
	/// Usado para confirmar email.
	/// </summary>
    /// <param name="token"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/confirm-email?token={token}
	///		
	/// </remarks>
	/// <response code="200">Envia email para confirmação.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, Resource.ConfirmEmailErro);
		}
	}

    /// <summary>
	/// Usado para reenviar email.
	/// </summary>
    /// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/resend-confirm-email?id={id}
	///		
	/// </remarks>
	/// <response code="200">Reenvia email para confirmação.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
		}
	}


	/// <summary>
	/// Usado para verificar email para redefinir senha.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /auth/exists
	///		{
	///			"Email": "email@gmail.com"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Verifica o email.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, Resource.ForgotPasswordErro);
		}
	}

    /// <summary>
	/// Usado para obter usuário atual.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/user
	///		
	/// </remarks>
	/// <response code="200">Obtém usuário atual.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[Authorize]
	[HttpGet]
	[Route("/auth/user")]
	public async Task<IActionResult> GetCurrentUser()
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			var user = await _authService.GetUserByIdAsync(userId);
			var isOrganizer = await _organizerService.IsOrganizerAnywhereValidation(userId);
			
			var organizer = new Organizer();
			if (isOrganizer)
				organizer = await _organizerService.IsUserAnOrganizerValidation(userId);
			
			return ApiOk(new 
			{
				email = user.Email,
				userName = user.Username,
				bio = user.Bio,
				picture = user.Picture,
				profileImg = user.Picture,
				name = user.Name,
				id = user.Id,
				championshipId = organizer?.ChampionshipId,
				isOrganizer,
				isSubOrganizer = !organizer?.MainOrganizer,
				teamManagementId = user.TeamManagementId,
				role = user.Role
			});
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message);
		}
	}

    /// <summary>
	/// Usado para reenviar email para usuário redefinir a senha.
	/// </summary>
	/// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/resend-forgot-password?id={id}
	///		
	/// </remarks>
	/// <response code="200">Reenvia email para usuário redefinir a senha.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, GenericError.GenericErrorMessage);
		}
	}

    /// <summary>
	/// Usado para redefinir senha por GET.
	/// </summary>
	/// <param name="token"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/reset-password?token={token}
	///		
	/// </remarks>
	/// <response code="200">Redefine a senha.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[HttpGet]
	[Route("/auth/reset-password")] 
	public async Task<IActionResult> ResetPassword(string token)
	{
		try
		{
			return ApiOk(_authService.ConfirmResetPassword(token));
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(Resource.InvalidPasswordToken, GenericError.GenericErrorMessage);
		}
	}

	/// <summary>
	/// Usado para redefinir senha por POST.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /auth/reset-password
	///		{
	///			"Email": "email@gmail.com",
	///			"Password": "Abc1_"
	///		}
	///		
	/// </remarks>
	/// <response code="200">Redefine a senha.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
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
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message, Resource.ResetPasswordErro);
		}
	}

    /// <summary>
	/// Usado para verificar se usuário possui CPF ou CNPJ cadastrado.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/cpf
	///		
	/// </remarks>
	/// <response code="200">Retorna se o usuário tem ou não CPF ou CNPJ cadastrado.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[Authorize]
	[HttpGet]
	[Route("/auth/cpf")]
	public async Task<IActionResult> CurrentUserHasCpf()
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			var hasCpfOrCnpj = await _authService.UserHasCpfValidationAsync(userId);
			hasCpfOrCnpj = hasCpfOrCnpj || await _authService.UserHasCnpjValidationAsync(userId);
			
			return ApiOk(hasCpfOrCnpj);
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message);
		}
	}

	/// <summary>
	/// Usado para adicionar CPF ou CNPJ para usuário atual.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		POST /auth/cpf
	///		"78641357068"
	///		
	/// </remarks>
	/// <response code="200">Cadastra CPF ou CNPJ para usuário atual.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[Authorize]
	[HttpPost]
	[Route("/auth/cpf")]
	public async Task<IActionResult> AddCpf([FromBody] string cpfOrCnpj)
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			
			List<string> resultados;
			var isCnpj = cpfOrCnpj.Length > 11;
			if (isCnpj)
				resultados = await _authService.AddCnpjUserValidationAsync(userId, cpfOrCnpj);
			else
				resultados = await _authService.AddCpfUserValidationAsync(userId, cpfOrCnpj);
			
			if (resultados.Any())
				return ApiBadRequest(resultados);
			
			return ApiOk(string.Format(Resource.AddCpfCPFVinculadoComSucesso, isCnpj ? "CNPJ" : "CPF"));
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(ex.Message);
		}
	}

    /// <summary>
	/// Usado para obter usuário por id.
	/// </summary>
	/// <param name="id"></param>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		GET /auth/{id}
	///		
	/// </remarks>
	/// <response code="200">Retorna o usuário conforme id.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[HttpGet]
	[Route("/auth/{id:guid}")]
	public async Task<IActionResult> GetById(Guid id)
	{
		try
		{
			return ApiOk(await _authService.GetUserByIdAsync(id));
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(Resource.GetByIdUsuarioNaoExiste);
		}
	}

    /// <summary>
	/// Usado para deletar usuário atual.
	/// </summary>
	/// <remarks>
	/// Exemplo de requisição:
	/// 
	///		DELETE /auth/user
	///		
	/// </remarks>
	/// <response code="200">Deleta o usuário atual.</response>
	/// <response code="401">Retorna uma falha indicando algum erro cometido na requisição.</response>
	/// <returns>
	/// </returns>
	[HttpDelete]
	[Authorize]
	[Route("/auth/user")]
	public async Task<IActionResult> Delete()
	{
		try
		{
			var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
			var user = await _authService.GetUserByIdAsync(userId);
			if(user.TeamManagementId != 0)
				await _woService.DeleteTeamValidation(user.TeamManagementId, user.Id);
			if (user.ChampionshipId != 0)
				await _organizerService.DeleteValidation(new() { ChampionshipId = user.ChampionshipId, OrganizerId = user.Id });

			await _authService.DeleteCurrentUserValidation(user);
			
			Response.Cookies.Delete("playoffs-token");
			Response.Cookies.Delete("playoffs-refresh-token");
			
			return ApiOk(Resource.DeleteUsuarioExcluidoComSucesso);
		}
		catch (ApplicationException ex)
		{
			await _error.HandleExceptionValidationAsync(HttpContext, ex);
			return ApiBadRequest(Resource.DeleteHouveErroExcluirUsuario);
		}
	}
}
