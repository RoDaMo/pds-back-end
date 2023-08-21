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
    private readonly ErrorLogService _error;
    private readonly RedisService _redisService;
    public ImageController(ImageService imageService, ErrorLogService error, RedisService redisService)
    {
        _imageService = imageService;
        _error = error;
        _redisService = redisService;
    }
    
    [HttpGet]
    [Route("/img/{id}")]
    public async Task<IActionResult> GetImage(string id)
    {
        var image = await _imageService.GetImage(id);

        HttpContext.Response.Headers["Cache-Control"] = "public, max-age=31536000";
        return File(image.Stream.ToArray(), image.ContentType, image.FileName + "." + image.Extension);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SendImage(IFormFile file, [FromForm]TypeUpload type)
    {
        try
        {
            await using var redis = await _redisService.GetDatabase();
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            
            var keyUser = $"rate-limit-image-{userId.ToString()}";

            var currentCount = await redis.GetValueAsync(keyUser);
            if (currentCount is null)
                await redis.SetAsync(keyUser, 0, TimeSpan.FromMinutes(3));
            else if (int.Parse(currentCount) >= 10)
                throw new ApplicationException(Resource.SendImageTooManyUploads);
            
            await redis.IncrementValueAsync(keyUser);
            
            await using var stream = file.OpenReadStream();
            
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
        catch (ApplicationException ex)
        {
            await _error.HandleExceptionValidationAsync(HttpContext, ex);
            return ApiBadRequest(ex.Message);
        }
    }
}