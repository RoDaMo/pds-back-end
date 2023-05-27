﻿using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayOffsApi.API;
using PlayOffsApi.Models;
using PlayOffsApi.Services;

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
        
        return File(image.Stream.ToArray(), image.Extension, image.FileName.ToString());
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SendImage(IFormFile file)
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
                Extension = file.FileName.Split('.')[1],
                FileName = Guid.NewGuid(),
                UserId = userId,
                ContentType = file.ContentType
            };
            
            await _imageService.SendImage(image);
            return ApiOk(image.FileName, message: "Imagem enviada com sucesso");
        }
        catch (ApplicationException e)
        {
            return ApiBadRequest(e.Message);
        }
    }
}