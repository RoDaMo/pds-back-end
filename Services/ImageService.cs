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
    private const string BucketName = "playoffs-armazenamento";
    private readonly AWSCredentials _awsCredentials = new EnvironmentVariablesAWSCredentials();
    private readonly string _mountPath = Environment.GetEnvironmentVariable("MOUNT_PATH");
    private static readonly Dictionary<string, string> ContentTypeMappings = new()
    {
        { "image/jpeg", ".jpg" },
        { "image/png", ".png" },
        { "image/gif", ".gif" },
        { "image/bmp", ".bmp" },
        { "image/tiff", ".tiff" },
        { "image/webp", ".webp" },
        { "application/pdf", ".pdf" }
    };

    private AmazonS3Client GetClient => new(_awsCredentials, RegionEndpoint.SAEast1);
    
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

    public async Task DownloadFilesFromS3()
    {
        var listRequest = new ListObjectsV2Request
        {
            BucketName = BucketName,
            MaxKeys = 1000 
        };

        ListObjectsV2Response response;
        using var client = GetClient;
        do
        {
            response = await client.ListObjectsV2Async(listRequest);
            foreach (var entry in response.S3Objects)
                await DownloadFile(entry.Key, client);
            
            listRequest.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated);
    }

    private async Task DownloadFile(string key, IAmazonS3 client)
    {
        var getObjectMetadataRequest = new GetObjectMetadataRequest
        {
            BucketName = BucketName,
            Key = key
        };

        var response = await client.GetObjectMetadataAsync(getObjectMetadataRequest);
        var contentType = new ContentType(response.Headers["Content-Type"]);
        var fileExtension = GetFileExtension(contentType);
        var fileNameWithExtension = Path.GetFileNameWithoutExtension(key) + fileExtension;
        var downloadPath = Path.Combine(_mountPath, fileNameWithExtension);
    
        using var fileTransferUtility = new TransferUtility(client);
        await fileTransferUtility.DownloadAsync(downloadPath, BucketName, key);
    }

    private static string GetFileExtension(ContentType type)
    {
        ContentTypeMappings.TryGetValue(type.MediaType, out var value);
        return value;
    }
}