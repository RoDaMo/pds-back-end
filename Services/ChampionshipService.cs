using pds_back_end.Controllers.Validations;
using pds_back_end.Models;

namespace pds_back_end.Services;

public class ChampionshipService
{
    private readonly DbService _dbService;

    public ChampionshipService(DbService dbService)
    {
        _dbService = dbService;
    }
    public async Task<List<string>> CreateValidationAsync(Championship championship)
    {
        var errorMessages = new List<string>();

        var championshipValidator = new ChampionshipValidator();
    
        var result = championshipValidator.Validate(championship);

        if (!result.IsValid)
        {
            errorMessages = result.Errors.Select(x => x.ErrorMessage).ToList();
            return errorMessages;
        }

        var result2 = await CreateSendAsync(championship);
        if(!result2)
        {
            errorMessages.Add("Erro ao cadastrar campeonato");
            return errorMessages;
        }

        return errorMessages;
    }

    public async Task<bool> CreateSendAsync(Championship championship)
    {
        try
        {
            var result = await _dbService.EditData(
                "INSERT INTO championships (id, name, prize, sportsid, initialdate, finaldate) VALUES (@Id, @Name, @Prize, @SportsId, @Initialdate, @Finaldate)",
                championship);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
        
    }
}
