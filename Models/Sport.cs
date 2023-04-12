namespace PlayOffsApi.Models;

public class Sport
{
    public int Id { get; set; }
    public string Name { get; set; }

    public Sport(string name)
    {
        Name = name;
    }

    public Sport() {}
}
