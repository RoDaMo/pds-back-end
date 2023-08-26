namespace PlayOffsApi.DTO;

public class LastResultsDTO
{
    public bool Tied { get; set; }
    public bool Won { get; set; }
    public bool Lose { get; set; }

    public LastResultsDTO()
    {
        Tied = false;
        Won = false;
        Lose  = false;
    }
}