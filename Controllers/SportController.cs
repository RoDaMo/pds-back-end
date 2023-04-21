using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/Sports")]
public class SportController : ApiBaseController
{
	private readonly SportService _sportService;

	public SportController(SportService sportService)
	{
		_sportService = sportService;
	}

	[HttpGet]
	public async Task<IActionResult> Index()
	{
		var sportList = new List<Sport>();
		try
		{
			sportList = await _sportService.GetAllValidationAsync();
			return ApiOk(sportList);
		}
		catch (ApplicationException ex)
		{
			return ApiBadRequest(sportList, ex.Message);
		}
	}
}

