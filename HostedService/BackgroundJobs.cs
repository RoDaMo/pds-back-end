using System.Linq.Expressions;
using System.Net.Mime;
using System.Text.Json;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;
using PlayOffsApi.Services;
using ServiceStack.Redis;

namespace PlayOffsApi.HostedService;

public class BackgroundJobs : BackgroundService, IBackgroundJobsService
{
    private readonly RedisService _redisService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private IRedisSubscriptionAsync _subscription;
    private CancellationToken _cts;
     private readonly ILogger<BackgroundJob> _logger;
    // private const string BucketName = "playoffs-armazenamento";
    // private readonly AWSCredentials _awsCredentials = new EnvironmentVariablesAWSCredentials();
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

    // private AmazonS3Client GetClient => new(_awsCredentials, RegionEndpoint.SAEast1);
    public BackgroundJobs(RedisService redisService, IServiceScopeFactory serviceScopeFactory, ILogger<BackgroundJob> logger)
    {
        _redisService = redisService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cts = stoppingToken;
        Task.Run(ExecutePeriodicallyAsync, stoppingToken);
        return StartBackgroundJobs();
    }

    private async Task StartBackgroundJobs()
    {
        await using var dataBase = await _redisService.GetDatabase();
        _subscription = await dataBase.CreateSubscriptionAsync(_cts);

        _subscription.OnMessageAsync += async (channel, message) => {
            try
            {
                var jobDeserialized = JsonSerializer.Deserialize<BackgroundJob>(message);

                ExecuteBackgroundJob(jobDeserialized);
            }
            catch (Exception e)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var error = scope.ServiceProvider.GetRequiredService<ErrorLogService>();
                await error.HandleExceptionValidationAsync(new DefaultHttpContext(), e);
            }
        };

        await _subscription.SubscribeToChannelsAsync("jobs");
    }

    private void ExecuteBackgroundJob(BackgroundJob jobDeserialized)
    {
        var paramList = (from param in jobDeserialized.Params
            let type = Type.GetType(param.Type)
            select param.Value.Deserialize(type!)).ToArray();

        GetType().GetMethod(jobDeserialized.MethodName)!.Invoke(this, paramList);
    }

    public async Task EnqueueJob(Expression<Func<Task>> methodExpression, TimeSpan? period = null)
    {
        var (methodName, parameters) = GetMethodDetails(methodExpression);
        _logger.LogInformation("Nome do método: {methodName}", methodName);
        _logger.LogInformation("Período: {period}", period);

        var jobObject = new BackgroundJob
        {
            MethodName = methodName, 
            Params = parameters.Select(param => new BackgroundJobParameter { Type = param.GetType().AssemblyQualifiedName, Value = JsonSerializer.SerializeToElement(param, param.GetType()) }).ToArray(),
        };

        var jobObjectSerialized = JsonSerializer.Serialize(jobObject);
        await using var database = await _redisService.GetDatabase();
        if (period is null)
        {
            await database.PublishMessageAsync("jobs", jobObjectSerialized, _cts);
            return;
        }

        var scheduledDate = DateTime.UtcNow.Add(period.Value);
        _logger.LogInformation("scheduledDate: {scheduledDate} ", scheduledDate);

        var unixTimestamp = (long)(scheduledDate - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        _logger.LogInformation("unixTimestamp: {unixTimestamp} ", unixTimestamp);
        await database.AddItemToSortedSetAsync("scheduled_jobs", jobObjectSerialized, unixTimestamp, _cts);
    }
    
    private static (string Name, object[] Parameters) GetMethodDetails(Expression<Func<Task>> expression)
    {
        if (expression.Body is not MethodCallExpression methodCall)
            throw new ArgumentException("Expression is not a method call", nameof(expression));
        
        var methodName = methodCall.Method.Name;
        var parameters = methodCall.Arguments.Select(arg => Expression.Lambda(arg).Compile().DynamicInvoke()).ToArray();
        return (methodName, parameters);
    }

    private async Task DequeueDueJobsAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var database = await _redisService.GetDatabase();
        
        var jobsTask = database.GetRangeFromSortedSetByLowestScoreAsync("scheduled_jobs", double.NegativeInfinity, now, _cts);
        var removalTask = database.RemoveRangeFromSortedSetByScoreAsync("scheduled_jobs", double.NegativeInfinity, now, _cts);
        
        var fetchedJobs = await jobsTask;
        await removalTask; 

        var jobs = fetchedJobs.Select(job => JsonSerializer.Deserialize<BackgroundJob>(job)).Where(backgroundJob => backgroundJob is not null).ToList();
        _logger.LogInformation("Quantidade de jobs: {jobsCount}", jobs.Count);
        
        foreach (var job in jobs)  
        {
            ExecuteBackgroundJob(job);
        }
    }
    
    private async Task ExecutePeriodicallyAsync()
    {
        while (true)
        {
            await DequeueDueJobsAsync();
            await Task.Delay(TimeSpan.FromHours(4), _cts);
        }
    }

    public async Task ChangeChampionshipStatusValidation(int championshipId, int status)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbService = scope.ServiceProvider.GetRequiredService<DbService>();
        var elasticService = scope.ServiceProvider.GetRequiredService<ElasticService>();
        var activityLogService = new ChampionshipActivityLogService(dbService);
        
        var activityFromChampionship = await activityLogService.GetAllFromChampionshipValidation(championshipId);
        await using var database = await _redisService.GetDatabase();
        
        var cancelJob = await database.GetValueAsync($"cancelJob_championship:{championshipId}", _cts);
        var championship = await dbService.GetAsync<Championship>("SELECT * FROM championships WHERE id = @id", new { championshipId });

        _logger.LogInformation(cancelJob);
        _logger.LogInformation("Data de início do camp: " + championship.InitialDate);

        if (cancelJob is not null)
            if (DateTime.Parse(cancelJob) != championship.InitialDate) return;

        var statusEnum = (ChampionshipStatus)status;

        _logger.LogInformation("Status que o campeonato irá ser setado: " + statusEnum);
        _logger.LogInformation("Se a lista de atividade do campeonato tem alguma coisa: " + activityFromChampionship.Any());

        if (activityFromChampionship.Any() && statusEnum == ChampionshipStatus.Inactive)
        {
            var lastActivity = activityFromChampionship.OrderByDescending(d => d.DateOfActivity).Last();
              _logger.LogInformation("Data da última atividade: " + lastActivity.DateOfActivity);
            if (DateTime.UtcNow - lastActivity.DateOfActivity < TimeSpan.FromDays(14)) return;
        }

        await ChangeChampionshipStatusSend(championshipId, statusEnum, dbService);
        championship.Status = statusEnum;
        
        var isDevelopment = Environment.GetEnvironmentVariable("IS_DEVELOPMENT");
        await elasticService._client.IndexAsync(championship, string.IsNullOrEmpty(isDevelopment) || isDevelopment == "false" ? "championships" : "championships-dev", _cts);
    }

    private static async Task ChangeChampionshipStatusSend(int championshipId, ChampionshipStatus status, DbService dbService) 
        => await dbService.EditData("UPDATE championships SET status = @status WHERE id = @id", new { id = championshipId, status });
    
    // public async Task DownloadFilesFromS3()
    // {
    //     var listRequest = new ListObjectsV2Request
    //     {
    //         BucketName = BucketName,
    //         MaxKeys = 1000 
    //     };
    //
    //     ListObjectsV2Response response;
    //     using var client = GetClient;
    //     do
    //     {
    //         response = await client.ListObjectsV2Async(listRequest, _cts);
    //         foreach (var entry in response.S3Objects)
    //             await DownloadFile(entry.Key, client);
    //         
    //         listRequest.ContinuationToken = response.NextContinuationToken;
    //     } while (response.IsTruncated);
    // }
    //
    // private async Task DownloadFile(string key, IAmazonS3 client)
    // {
    //     var getObjectMetadataRequest = new GetObjectMetadataRequest
    //     {
    //         BucketName = BucketName,
    //         Key = key
    //     };
    //
    //     var response = await client.GetObjectMetadataAsync(getObjectMetadataRequest, _cts);
    //     var contentType = new ContentType(response.Headers["Content-Type"]);
    //     var fileExtension = GetFileExtension(contentType);
    //     var fileNameWithExtension = Path.GetFileNameWithoutExtension(key) + fileExtension;
    //     var downloadPath = Path.Combine(_mountPath, fileNameWithExtension);
    //
    //     using var fileTransferUtility = new TransferUtility(client);
    //     await fileTransferUtility.DownloadAsync(downloadPath, BucketName, key, _cts);
    // }

    private static string GetFileExtension(ContentType type)
    {
        ContentTypeMappings.TryGetValue(type.MediaType, out var value);
        return value;
    }
}