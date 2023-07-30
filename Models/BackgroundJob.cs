namespace PlayOffsApi.Models;

public class BackgroundJob
{
    public string MethodName { get; set; }
    public BackgroundJobParameter[] Params { get; set; }
    public DateTime ScheduledDate { get; set; }
}