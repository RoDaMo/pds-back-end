using ServiceStack.Redis;

namespace PlayOffsApi.Services;

public class RedisService
{
  private readonly RedisManagerPool _redis;
  public RedisService(IWebHostEnvironment environment)
  {
    string url = "localhost:6379";

    if (environment.IsProduction()) 
      url = Environment.GetEnvironmentVariable("REDIS_URL");

    _redis = new RedisManagerPool(url);
  }

  public async Task<IRedisClientAsync> GetDatabase() => await _redis.GetClientAsync();
}