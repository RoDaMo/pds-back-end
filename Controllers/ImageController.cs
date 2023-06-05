using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using Resource = PlayOffsApi.Resources.Controllers.ImageController;

namespace PlayOffsApi.Controllers;

[Route("/img")]
[ApiController]
public class ImageController : ApiBaseController
{
    private readonly ImageService _imageService;
    public ImageController(ImageService imageService)
    {
        _imageService = imageService;
    }
    
    [HttpGet]
    [Route("/img/{id:guid}")]
    public async Task<IActionResult> GetImage(Guid id)
    {
        var image = await _imageService.GetImage(id);
        
        return File(image.Stream.ToArray(), image.ContentType, image.FileName + "." + image.Extension);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SendImage(IFormFile file, [FromForm]TypeUpload type)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            
            var image = new Image
            {
                Stream = memoryStream,
                Extension = file.FileName.Split('.').Last(),
                FileName = Guid.NewGuid(),
                UserId = userId,
                ContentType = file.ContentType
            };
            var erros = await _imageService.SendImage(image, type);
            return erros.Any() ? ApiBadRequest(erros) : ApiOk(image.FileName, message: Resource.SendImageImagemEnviadaSucesso);
        }
        catch (ApplicationException e)
        {
            return ApiBadRequest(e.Message);
        }
    }
}