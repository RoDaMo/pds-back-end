namespace PlayOffsApi.Models;

public class Image
{
    public Guid FileName { get; init; }
    public MemoryStream Stream { get; init; }
    public string Extension { get; init; }
    public Guid UserId { get; init; }
    public int OwnerId { get; init; }
    public string ContentType { get; init; }
}