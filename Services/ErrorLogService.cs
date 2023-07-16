using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class ErrorLogService
{
    private readonly DbService _dbService;
    public ErrorLogService(DbService dbService)
    {
        _dbService = dbService;
    }
    
    public async Task HandleExceptionValidationAsync(HttpContext context, Exception exception) =>
        await HandleExceptionValidationSend(new ErrorLog
        {
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            TimeOfError = DateTime.UtcNow
        });

    private async Task HandleExceptionValidationSend(ErrorLog log) =>
        await _dbService.EditData("INSERT INTO ErrorLog (Message, StackTrace, TimeOfError) VALUES (@Message, @StackTrace, @TimeOfError)", log);

    public async Task<List<ErrorLog>> GetAllValidationAsync() => await GetAllSendAsync();

    private async Task<List<ErrorLog>> GetAllSendAsync() => await _dbService.GetAll<ErrorLog>("SELECT id, message, stacktrace, timeoferror FROM errorlog ORDER BY timeoferror DESC", null);
}