namespace PlayOffsApi.DTO;

public class TeamDTO
{
	public string Emblem { get; set; }
    public string UniformHome { get; set; }
    public string UniformWay{ get; set; }
    public int SportsId { get; set; }
    public string Name { get; set; }
    public string Cpf { get; set; }
    public Guid ManagersId { get; set; }
}