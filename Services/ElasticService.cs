using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Resource = PlayOffsApi.Resources.Generic;

namespace PlayOffsApi.Services;

public class ElasticService
{
    public readonly ElasticsearchClient _client;
    public ElasticService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        ElasticsearchClientSettings settings;
        if (environment.IsProduction())
        {
            var CLOUD_ID = Environment.GetEnvironmentVariable("CLOUD_ID");
            var API_KEY = Environment.GetEnvironmentVariable("ELASTIC_API_KEY");
            var ELASTIC_USER = Environment.GetEnvironmentVariable("ELASTIC_USER");
            var ELASTIC_PASSWORD = Environment.GetEnvironmentVariable("ELASTIC_PASSWORD");

            settings = new ElasticsearchClientSettings(CLOUD_ID, new ApiKey(API_KEY))
              .Authentication(new BasicAuthentication(ELASTIC_USER, ELASTIC_PASSWORD));
        }
        else
        {
            settings = new ElasticsearchClientSettings(new Uri(configuration.GetValue<string>("ElasticURI")))
              .CertificateFingerprint(configuration.GetValue<string>("Fingerprint"))
              .Authentication(new BasicAuthentication("elastic", configuration.GetValue<string>("Password")))
              .EnableDebugMode()
              .PrettyJson();
        }

        _client = new ElasticsearchClient(settings);
    }

    public async Task<string> GetClusterHealth()
    {
        var resultado = await _client.Cluster.HealthAsync();
        return resultado.IsSuccess().ToString();
    }

    public async Task<List<T>> SearchAsync<T>(Action<SearchRequestDescriptor<T>> request)
    {
        var resposta = await _client.SearchAsync(request);

        if (!resposta.IsValidResponse)
            throw new ApplicationException(Resource.GenericErrorMessage);

        return resposta.Documents.ToList();
    }
}