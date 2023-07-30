using System.Text.Json;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class BackgroundService
{
    private readonly RedisService _redisService;	
    private readonly DbService _dbService;
    private readonly ElasticService _elasticService;
    public BackgroundService(RedisService redisService, DbService dbService, ElasticService elasticService)
    {
        _redisService = redisService;
        _dbService = dbService;
        _elasticService = elasticService;

        Task.Run(StartBackgroundJobs);
    }

    private async Task StartBackgroundJobs()
    {
        var dataBase = await _redisService.GetDatabase();

        while (true)
        {
            try
            {
                var job = await dataBase.PopItemFromListAsync("jobs");
                if (job is not null)
                {
                    var jobDeserialized = JsonSerializer.Deserialize<BackgroundJob>(job);
                    
                    if (!jobDeserialized.ScheduledDate.Equals(DateTime.MinValue) && jobDeserialized.ScheduledDate > DateTime.UtcNow)
                    {
                        await dataBase.PushItemToListAsync("jobs", job);
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        continue;
                    }
                    
                    var paramList = (from param in jobDeserialized.Params let type = Type.GetType(param.Type) select param.Value.Deserialize(type!)).ToArray();

                    GetType().GetMethod(jobDeserialized.MethodName)!.Invoke(this, paramList);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception e)
            {
                await HandleExceptionAsync(e);
            }
        }
    }

    public async Task EnqueueJob(string methodName, object[] parameters, TimeSpan? period = null)
    {

        var jobObject = new BackgroundJob
        {
            MethodName = methodName, 
            Params = parameters.Select(param => new BackgroundJobParameter { Type = param.GetType().AssemblyQualifiedName, Value = JsonSerializer.SerializeToElement(param, param.GetType()) }).ToArray(),
            ScheduledDate =  period is null ? DateTime.MinValue : DateTime.UtcNow.Add(period.Value)
        };

        var jobObjectSerialized = JsonSerializer.Serialize(jobObject);
        var database = await _redisService.GetDatabase();
        
        await database.PushItemToListAsync("jobs", jobObjectSerialized);
    }

    // cant use scoped service inside singleton
    private async Task HandleExceptionAsync(Exception exception) =>
        await _dbService.EditData("INSERT INTO ErrorLog (Message, StackTrace, TimeOfError) VALUES (@Message, @StackTrace, @TimeOfError)", new ErrorLog
        {
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            TimeOfError = DateTime.UtcNow
        });

    
    public async Task ChangeChampionshipStatusValidation(int championshipId, int status)
    {
        var activityLogService = new ChampionshipActivityLogService(_dbService);
        var activityFromChampionship = await activityLogService.GetAllFromChampionshipValidation(championshipId);
        
        var database = await _redisService.GetDatabase();
        var cancelJob = await database.GetValueAsync($"cancelJob_championship:{championshipId}");
        var championship = await _dbService.GetAsync<Championship>("SELECT * FROM championships WHERE id = @id", new { championshipId });

        if (cancelJob is not null)
        {
            if (DateTime.Parse(cancelJob) != championship.InitialDate) return;
        }

        var statusEnum = (ChampionshipStatus)status;
        if (activityFromChampionship.Any() && statusEnum == ChampionshipStatus.Inactive)
        {
            var lastActivity = activityFromChampionship.OrderByDescending(d => d.DateOfActivity).Last();
            if (DateTime.UtcNow - lastActivity.DateOfActivity < TimeSpan.FromDays(14)) return;
        }

        await ChangeChampionshipStatusSend(championshipId, statusEnum);
        championship.Status = statusEnum;
        await _elasticService._client.IndexAsync(championship, "championships");
    }

    private async Task ChangeChampionshipStatusSend(int championshipId, ChampionshipStatus status) 
        => await _dbService.EditData("UPDATE championships SET status = @status WHERE id = @id", new { id = championshipId, status });
}