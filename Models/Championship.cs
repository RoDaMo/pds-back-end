namespace pds_back_end.Models;

public class Championship
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool Prize { get; set; }
    public DateTime InitialDate { get; set; }
    public DateTime FinalDate { get; set; }
    public int SportsId { get; set; }

    public Championship(string name, bool prize, DateTime initialDate, DateTime finalDate)
    {
        Name = name;
        Prize = prize;
        InitialDate = initialDate;
        FinalDate = finalDate;
    }

    public Championship() {}
}
