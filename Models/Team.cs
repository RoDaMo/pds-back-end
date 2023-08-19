namespace PlayOffsApi.Models;

public class Team
{
    public int Id { get; set; }
	public string Emblem { get; set; }
    public string UniformHome { get; set; }
    public string UniformAway{ get; set; }
    public bool Deleted{ get; set; }
    public int SportsId { get; set; }
    public string Name { get; set; }
    public User Technician { get; set; }
   

	public Team(string emblem, string uniformHome, string uniformAway, string name)
	{
		Emblem = emblem;
        UniformHome = uniformHome;
        UniformAway = uniformAway;
        Deleted = false;
        Name = name;
	}

    public Team(string emblem, string uniformHome, string uniformAway, int sportsId, string name)
	{
		Emblem = emblem;
        UniformHome = uniformHome;
        UniformAway = uniformAway;
        Deleted = false;
        SportsId = sportsId;
        Name = name;
	}

	public Team() { }
}
