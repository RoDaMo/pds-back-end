using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using System.Collections.Specialized;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.Serialization;
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
			var ELASTIC_KEY = Environment.GetEnvironmentVariable("ELASTIC_KEY");
			var ELASTIC_URL = Environment.GetEnvironmentVariable("ELASTIC_URL");

			settings = new ElasticsearchClientSettings(new Uri(ELASTIC_URL))
				.GlobalHeaders(new NameValueCollection
				{
					{ "Authorization", ELASTIC_KEY }
				});
		}
		else
		{
			settings = new ElasticsearchClientSettings(new Uri(configuration.GetValue<string>("ElasticURI")))
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

	public async Task<SearchResponse<T>> SearchAsync<T>(Action<SearchRequestDescriptor<T>> request)
	{
		var response = await _client.SearchAsync(request);

		if (!response.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);

		return response;
	}

	public async Task<PointInTimeReference> OpenPointInTimeAsync(Indices index)
	{
		var response = await _client.OpenPointInTimeAsync(index, config => config.KeepAlive(12000));
		
		if (!response.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);

		return new() { Id = response.Id, KeepAlive = 12000 };
	}
	public PointInTimeReference OpenPointInTime(Indices index)
	{
		var response = _client.OpenPointInTime(index, config => config.KeepAlive(2));
		
		if (!response.IsValidResponse)
			throw new ApplicationException(Resource.GenericErrorMessage);

		return new() { Id = response.Id, KeepAlive = 2 };
	}
}