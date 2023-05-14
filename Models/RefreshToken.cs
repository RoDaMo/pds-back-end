namespace PlayOffsApi.Models;

public class RefreshToken
{
    public Guid Token { get; set; }
    public Guid UserId { get; set; }

    public DateTime ExpirationDate { get; set; }
}