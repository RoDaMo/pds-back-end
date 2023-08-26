namespace PlayOffsApi.DTO;

public class PlayerGoalsSummaryDTO
{
    public Guid PlayerIdOrTempId { get; set; }
    public int Goals { get; set; }
}