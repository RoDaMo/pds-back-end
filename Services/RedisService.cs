using StackExchange.Redis;

namespace pds_back_end.Services;

public class RedisService
{
  private readonly IConnectionMultiplexer _redis;
  public RedisService(IWebHostEnvironment environment)
  {
    string url = "localhost:6379";

    if (environment.IsProduction()) 
      url = Environment.GetEnvironmentVariable("REDIS_URL");

    _redis = ConnectionMultiplexer.Connect(url);
  }

  public IDatabase Database { get => _redis.GetDatabase(); }
}