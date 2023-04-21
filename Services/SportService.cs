using PlayOffsApi.Models;

namespace PlayOffsApi.Services;

public class SportService
{
  private readonly DbService _dbService;

  public SportService(DbService dbService)
  {
    _dbService = dbService;
  }

  public async Task<List<Sport>> GetAllValidationAsync() => await GetAllSendAsync();

  public async Task<List<Sport>> GetAllSendAsync() => await _dbService.GetAll<Sport>("SELECT * FROM sports", new { });
}
