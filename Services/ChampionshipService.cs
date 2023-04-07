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

        await CreateSendAsync(championship);

        return errorMessages;
    }

    public async Task CreateSendAsync(Championship championship)
    {
        await _dbService.EditData(
            "INSERT INTO championships (name, prize, sportsid, initialdate, finaldate) VALUES (@Name, @Prize, @SportsId, @Initialdate, @Finaldate)",
            championship);
    }
}
