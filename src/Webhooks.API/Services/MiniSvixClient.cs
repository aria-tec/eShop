using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Webhooks.API.Services;

public class MiniSvixClient(
    HttpClient httpClient, 
    ILogger<MiniSvixClient> logger, 
    IOptions<MiniSvixOptions>? options = null) : IMiniSvixClient
{
    public const string DefaultTenantId = "eshop";
    public const string HeaderTenantId = "X-Tenant-ID";
    public const string HeaderIdempotencyKey = "X-Idempotency-Key";

    private readonly string _tenantId = options?.Value?.TenantId ?? DefaultTenantId;

    public async Task<string> IngestEventAsync(string eventType, object payload, Guid idempotencyKey, CancellationToken cancellationToken = default)
    {
        var requestObj = new IngestEventRequest(eventType, payload);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/events")
        {
            Content = JsonContent.Create(requestObj)
        };

        request.Headers.Add(HeaderTenantId, _tenantId);
        request.Headers.Add(HeaderIdempotencyKey, idempotencyKey.ToString());


        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IngestEventResponse>(cancellationToken: cancellationToken);
        logger.LogInformation("Successfully ingested event {EventType} with ID {EventId} into Mini-Svix", eventType, result?.Id);
        return result?.Id ?? string.Empty;
    }

    public async Task<MiniSvixEndpoint> CreateEndpointAsync(string url, string secret, int rateLimit = 100, CancellationToken cancellationToken = default)
    {
        var requestObj = new CreateEndpointRequest(url, secret, rateLimit);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/endpoints")
        {
            Content = JsonContent.Create(requestObj)
        };

        request.Headers.Add(HeaderTenantId, _tenantId);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MiniSvixEndpoint>(cancellationToken: cancellationToken);
        logger.LogInformation("Successfully registered endpoint {EndpointUrl} with ID {EndpointId} in Mini-Svix", url, result?.Id);
        return result ?? throw new InvalidOperationException("Failed to deserialize MiniSvixEndpoint response.");
    }

    public async Task<IReadOnlyList<MiniSvixEndpoint>> ListEndpointsAsync(CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/endpoints");
        request.Headers.Add(HeaderTenantId, _tenantId);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<MiniSvixEndpoint>>(cancellationToken: cancellationToken);
        return result ?? [];
    }
}
