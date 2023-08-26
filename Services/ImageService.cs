using System.Net.Mime;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.StaticFiles;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using Resource = PlayOffsApi.Resources.Services.ImageService;

namespace PlayOffsApi.Services;

public partial class ImageService
{
    private readonly string _mountPath = Environment.GetEnvironmentVariable("MOUNT_PATH");

    public async Task<List<string>> SendImage(Image file, TypeUpload type)
    {
        var results = ValidateUpload(file, type);
        if (results.Any())
            return results;

        var filePath = Path.Combine(_mountPath, file.FileName + "." + file.Extension);
        await using var stream = File.Create(filePath);
        file.Stream.Position = 0;
        await file.Stream.CopyToAsync(stream);
        
        return new List<string>();
    }

    private static List<string> ValidateUpload(Image file, TypeUpload type)
    {
        var imgRegex = ImgRegex();
        var fileRegex = PdfRegex();
        var extension = file.Extension.ToLower();
        var returnValue = new List<string>();
        switch (type)
        {
            case TypeUpload.ChampionshipLogo:
            case TypeUpload.TeamLogo:
            case TypeUpload.TeamUniform:
            case TypeUpload.UserLogo:
            {
                if (ConvertBytesToMegabytes(file.Stream.Length) > 5)
                    returnValue.Add(Resource.ValidateUploadArquivoGrandeDemais);
                
                if (!imgRegex.IsMatch(extension))
                    returnValue.Add(Resource.ValidateUploadInvalidFileType);
                        
                return returnValue;
            }
            case TypeUpload.ChampionshipRule:
            {
                if (ConvertBytesToMegabytes(file.Stream.Length) > 20)
                    returnValue.Add(Resource.ValidateUploadArquivoGrandeDemais);
                
                if (!fileRegex.IsMatch(extension))
                    returnValue.Add(Resource.ValidateUploadInvalidFileType);
                        
                return returnValue;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public async Task<Image> GetImage(string fileName)
    {
        if (fileName.Split('.').Length == 1)
        {
            var files = Directory.GetFiles(_mountPath, fileName + ".*");
            fileName = Path.GetFileName(files[0]);
        }
        
        var filePath = Path.Combine(_mountPath, fileName);
        new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var contentType);
        
        await using var imageStream = new MemoryStream(await File.ReadAllBytesAsync(filePath));
        var fileNameAndExtension = fileName.Split('.');
        var (fileGuid, fileExtension) = (fileNameAndExtension[0], fileNameAndExtension[1]);

        return new Image
        {
            Stream = imageStream,
            FileName = Guid.Parse(fileGuid),
            ContentType = contentType,
            Extension = fileExtension
        };
    }

    [GeneratedRegex("^(jpg|jpeg|png|gif|bmp|tiff|webp)$")]
    private static partial Regex ImgRegex();
    [GeneratedRegex("^(pdf)$")]
    private static partial Regex PdfRegex();

    private static double ConvertBytesToMegabytes(long bytes) => (bytes / 1024f) / 1024f;
}