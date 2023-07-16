namespace PlayOffsApi.Models;

public class ErrorLog
{
    public int Id { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }
    public DateTime TimeOfError { get; set; }
}