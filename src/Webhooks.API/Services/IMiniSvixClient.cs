using System.Text.Json.Serialization;

namespace Webhooks.API.Services;

public interface IMiniSvixClient
{
    Task<string> IngestEventAsync(string eventType, object payload, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<MiniSvixEndpoint> CreateEndpointAsync(string url, string secret, int rateLimit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MiniSvixEndpoint>> ListEndpointsAsync(CancellationToken cancellationToken = default);
}

public record MiniSvixEndpoint(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("tenant_id")] string TenantId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("secret")] string Secret,
    [property: JsonPropertyName("rate_limit")] int RateLimit,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt
);

public record IngestEventRequest(
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("payload")] object Payload
);

public record IngestEventResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt
);

public record CreateEndpointRequest(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("secret")] string Secret,
    [property: JsonPropertyName("rate_limit")] int RateLimit
);

public class MiniSvixOptions
{
    public string Url { get; set; } = "http://localhost:8080";
    public string TenantId { get; set; } = "eshop";
    public string DefaultSecret { get; set; } = "whsec_eshop_demo_secret_2026";
}

