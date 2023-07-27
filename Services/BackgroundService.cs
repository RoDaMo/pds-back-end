using System.Text.Json;
using PlayOffsApi.Enum;
using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class BackgroundService
{
    private readonly RedisService _redisService;	
    private readonly DbService _dbService;
    public BackgroundService(RedisService redisService, DbService dbService)
    {
        _redisService = redisService;
        _dbService = dbService;

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
    
    public async Task EnqueueJob(string methodName, object[] parameters)
    {
        var jobObject = new BackgroundJob
        {
            MethodName = methodName, 
            Params = parameters.Select(param => new BackgroundJobParameter { Type = param.GetType().AssemblyQualifiedName, Value = JsonSerializer.SerializeToElement(param, param.GetType()) }).ToArray()
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
        => await ChangeChampionshipStatusSend(championshipId, (ChampionshipStatus)status);

    private async Task ChangeChampionshipStatusSend(int championshipId, ChampionshipStatus status) 
        => await _dbService.EditData("UPDATE championships SET status = @status WHERE id = @id", new { id = championshipId, status });
}