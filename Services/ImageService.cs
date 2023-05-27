using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class ImageService
{
    private const string BucketName = "playoffs-armazenamento";
    private readonly AWSCredentials _awsCredentials = new EnvironmentVariablesAWSCredentials();
    
    private AmazonS3Client GetClient => new(_awsCredentials, RegionEndpoint.SAEast1);
    
    public async Task SendImage(Image file)
    {
        using var client = GetClient;
        var uploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = BucketName,
            Key = file.FileName.ToString(),
            InputStream = file.Stream,
            ContentType = file.ContentType
        };
        
        await new TransferUtility(client).UploadAsync(uploadRequest);
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
}