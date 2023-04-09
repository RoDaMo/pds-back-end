using Microsoft.AspNetCore.Mvc;
using pds_back_end.API;
using pds_back_end.Services;
using pds_back_end.Models;

namespace pds_back_end.Controllers;

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

