using System.Text.Json;
using Webhooks.API.Model;

namespace Webhooks.API.Services;

public class WebhooksSender(IMiniSvixClient miniSvixClient, ILogger<WebhooksSender> logger) : IWebhooksSender
{
    public async Task SendAll(IEnumerable<WebhookSubscription> receivers, WebhookData data)
    {
        // Event type normalization for Mini-Svix
        var eventType = data.Type switch
        {
            "OrderPaid" => "order.paid",
            "OrderShipped" => "order.shipped",
            "CatalogItemPriceChanged" => "catalog.price_changed",
            _ => data.Type.ToLowerInvariant()
        };

        object payloadObj;
        try
        {
            payloadObj = JsonSerializer.Deserialize<JsonElement>(data.Payload);
        }
        catch
        {
            payloadObj = data.Payload;
        }

        // Extract deterministic Idempotency Key from IntegrationEvent.Id if present, else new Guid
        var idempotencyKey = Guid.NewGuid();
        if (payloadObj is JsonElement jsonElement)
        {
            if (jsonElement.TryGetProperty("Id", out var idProp) && idProp.TryGetGuid(out var guidVal))
            {
                idempotencyKey = guidVal;
            }
            else if (jsonElement.TryGetProperty("id", out var idLowerProp) && idLowerProp.TryGetGuid(out var guidLowerVal))
            {
                idempotencyKey = guidLowerVal;
            }
        }

        logger.LogInformation("Delegating webhook dispatch for event {EventType} (IdempotencyKey: {IdempotencyKey}) to Mini-Svix engine", eventType, idempotencyKey);
        await miniSvixClient.IngestEventAsync(eventType, payloadObj, idempotencyKey);
    }
}
