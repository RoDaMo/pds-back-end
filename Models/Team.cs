namespace PlayOffsApi.Models;

public class Team
{
    public int Id { get; set; }
	public string Emblem { get; set; }
    public string UniformHome { get; set; }
    public string UniformWay{ get; set; }
    public bool Deleted{ get; set; }
    public int SportsId { get; set; }

	public Team(string emblem, string uniformHome, string uniformWay)
	{
		Emblem = emblem;
        UniformHome = uniformHome;
        UniformWay = uniformWay;
        Deleted = false;
	}

	public Team() { }
}
