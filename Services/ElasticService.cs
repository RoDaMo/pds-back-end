using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace pds_back_end.Services;

public class ElasticService 
{
  private readonly ElasticsearchClient _client;
  public ElasticService(IConfiguration configuration, IWebHostEnvironment environment)
  {
    ElasticsearchClientSettings settings;
    if (environment.IsProduction()) 
    {
      var cloudID = Environment.GetEnvironmentVariable("CLOUD_ID");
      var apiKey = Environment.GetEnvironmentVariable("ELASTIC_API_KEY");
      settings = new ElasticsearchClientSettings(cloudID, new ApiKey(apiKey));
    }
    else 
    {
      settings = new ElasticsearchClientSettings(new Uri("https://localhost:9200"))
        .CertificateFingerprint(configuration.GetValue<string>("Fingerprint"))
        .Authentication(new BasicAuthentication("elastic", configuration.GetValue<string>("Password")));
    }

    _client = new ElasticsearchClient(settings);
  }

  public async Task<string> GetClusterHealth() 
  {
    var resultado = await _client.Cluster.HealthAsync();
    return resultado.IsSuccess().ToString();
  } 
}