using ServiceStack.Redis;

namespace PlayOffsApi.Services;

public class RedisService
{
  private readonly RedisManagerPool _redis;
  public RedisService(IWebHostEnvironment environment)
  {
    string url = "redis://default:subkZye2ZCf1dm1cC2w8@containers-us-west-200.railway.app:6689";

    if (environment.IsProduction()) 
      url = Environment.GetEnvironmentVariable("REDIS_URL");

    // _redis = ConnectionMultiplexer.Connect(url);
    _redis = new RedisManagerPool(url);
  }

  public async Task<IRedisClientAsync> GetDatabase() => await _redis.GetClientAsync();
}