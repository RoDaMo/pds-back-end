using System.Linq.Expressions;
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
    public BackgroundJobs(RedisService redisService, IServiceScopeFactory serviceScopeFactory)
    {
        _redisService = redisService;
        _serviceScopeFactory = serviceScopeFactory;
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
        var unixTimestamp = (long)(scheduledDate - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
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

        if (cancelJob is not null)
            if (DateTime.Parse(cancelJob) != championship.InitialDate) return;

        var statusEnum = (ChampionshipStatus)status;
        if (activityFromChampionship.Any() && statusEnum == ChampionshipStatus.Inactive)
        {
            var lastActivity = activityFromChampionship.OrderByDescending(d => d.DateOfActivity).Last();
            if (DateTime.UtcNow - lastActivity.DateOfActivity < TimeSpan.FromDays(14)) return;
        }

        await ChangeChampionshipStatusSend(championshipId, statusEnum, dbService);
        championship.Status = statusEnum;
        await elasticService._client.IndexAsync(championship, "championships", _cts);
    }

    private static async Task ChangeChampionshipStatusSend(int championshipId, ChampionshipStatus status, DbService dbService) 
        => await dbService.EditData("UPDATE championships SET status = @status WHERE id = @id", new { id = championshipId, status });
}