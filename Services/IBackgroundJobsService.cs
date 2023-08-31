using System.Linq.Expressions;

namespace PlayOffsApi.Services;

public interface IBackgroundJobsService
{
    public Task EnqueueJob(Expression<Func<Task>> methodExpression, TimeSpan? period = null);
    public Task ChangeChampionshipStatusValidation(int championshipId, int status);
    // public Task DownloadFilesFromS3();
}