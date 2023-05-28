using System.Text.RegularExpressions;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public partial class ImageService
{
    private const string BucketName = "playoffs-armazenamento";
    private readonly AWSCredentials _awsCredentials = new EnvironmentVariablesAWSCredentials();
    
    private AmazonS3Client GetClient => new(_awsCredentials, RegionEndpoint.SAEast1);
    
    public async Task<List<string>> SendImage(Image file, TypeUpload type)
    {
        var results = ValidateUpload(file, type);
        if (results.Any())
            return results;

        using var client = GetClient;
        var uploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = BucketName,
            Key = file.FileName.ToString(),
            InputStream = file.Stream,
            ContentType = file.ContentType
        };
        
        await new TransferUtility(client).UploadAsync(uploadRequest);
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
            case TypeUpload.UserLogo:
            {
                if (ConvertBytesToMegabytes(file.Stream.Length) > 5)
                    returnValue.Add("Arquivo grande demais.");
                
                if (!imgRegex.IsMatch(extension))
                    returnValue.Add("Tipo de arquivo inválido.");
                        
                return returnValue;
            }
            case TypeUpload.ChampionshipRule:
            {
                if (ConvertBytesToMegabytes(file.Stream.Length) > 20)
                    returnValue.Add("Arquivo grande demais.");
                
                if (!fileRegex.IsMatch(extension))
                    returnValue.Add("Tipo de arquivo inválido.");
                        
                return returnValue;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public async Task<Image> GetImage(Guid fileName)
    {
        using var client = GetClient;
        var responseTask = client.GetObjectAsync(new()
        {
            BucketName = BucketName,
            Key = fileName.ToString(),
        });
        
        using var response = await responseTask;
        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream);
        return new Image
        {
            Stream = memoryStream,
            FileName = Guid.Parse(response.Key),
            Extension = response.Headers.ContentType
        };
    }

    [GeneratedRegex("^(jpg|jpeg|png|gif|bmp|tiff|webp)$")]
    private static partial Regex ImgRegex();
    [GeneratedRegex("^(pdf)$")]
    private static partial Regex PdfRegex();

    private static double ConvertBytesToMegabytes(long bytes) => (bytes / 1024f) / 1024f;
}