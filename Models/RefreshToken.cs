namespace PlayOffsApi.Models;

public class RefreshToken
{
    public Guid Token { get; init; }
    public Guid UserId { get; init; }

    public DateTime ExpirationDate { get; init; }
}