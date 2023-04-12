using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Services;
using PlayOffsApi.Models;

namespace PlayOffsApi.Controllers;

[ApiController]
[Route("/Sports")]
public class SportController : ControllerBase
{
    private readonly SportService _sportService;

    public SportController(SportService sportService)
    {
        _sportService = sportService;
    }

    [HttpGet]
    public async Task<ApiResponse<List<Sport>>> Index() 
    {
        var sportList = new List<Sport>();
        try
        {
            sportList = await _sportService.GetAllValidationAsync();
            return new() { Succeed = true, Results = sportList};
        }
        catch (ApplicationException ex)
        {
            return new() { Succeed = false, Message = ex.Message, Results = sportList};
        }
    } 
}

