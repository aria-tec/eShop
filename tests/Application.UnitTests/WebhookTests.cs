using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Webhooks.API.Model;
using Webhooks.API.Services;

namespace eShop.Application.UnitTests;

[TestClass]
public class WebhookTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SubscriptionRequestRejectsInvalidUrlsAndEvent()
    {
        var request = new WebhookSubscriptionRequest
        {
            Url = "not-a-url",
            GrantUrl = "also-not-a-url",
            Event = "not-an-event"
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        Assert.IsFalse(valid);
        Assert.HasCount(3, results);
    }

    [TestMethod]
    public async Task SenderDelegatesEventIngestionToMiniSvixClient()
    {
        var miniSvixClient = Substitute.For<IMiniSvixClient>();
        var sender = new WebhooksSender(miniSvixClient, NullLogger<WebhooksSender>.Instance);
        var receivers = new[]
        {
            new WebhookSubscription
            {
                DestUrl = "https://receiver.test/hook",
                Token = "token",
                Type = WebhookType.OrderPaid
            }
        };

        var eventId = Guid.NewGuid();
        var samplePayload = new { Id = eventId, OrderId = 12345, Status = "Paid" };
        var data = new WebhookData(WebhookType.OrderPaid, samplePayload);

        await sender.SendAll(receivers, data);

        await miniSvixClient.Received(1).IngestEventAsync(
            Arg.Is<string>(t => t == "order.paid"),
            Arg.Any<object>(),
            Arg.Is<Guid>(k => k == eventId),
            Arg.Any<CancellationToken>()
        );
    }

    [TestMethod]
    public async Task MiniSvixClientSendsHeadersAndIngestPayload()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("{\"id\":\"evt_12345\",\"status\":\"accepted\",\"created_at\":\"2026-08-20T22:00:00Z\"}", System.Text.Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var client = new MiniSvixClient(httpClient, NullLogger<MiniSvixClient>.Instance);

        var idempotencyKey = Guid.NewGuid();
        var eventId = await client.IngestEventAsync("order.paid", new { order_id = 999 }, idempotencyKey, TestContext.CancellationToken);

        Assert.AreEqual("evt_12345", eventId);
        Assert.HasCount(1, handler.Requests);

        var request = handler.Requests[0];
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("/api/v1/events", request.RequestUri!.AbsolutePath);
        Assert.AreEqual("eshop", request.Headers.GetValues("X-Tenant-ID").Single());
        Assert.AreEqual(idempotencyKey.ToString(), request.Headers.GetValues("X-Idempotency-Key").Single());

        var content = await request.Content!.ReadAsStringAsync(TestContext.CancellationToken);
        Assert.Contains("order.paid", content);
        Assert.Contains("999", content);
    }

    [TestMethod]
    public async Task MiniSvixClientCreatesEndpoint()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"id\":\"ep_987\",\"tenant_id\":\"eshop\",\"url\":\"http://test.com/wh\",\"secret\":\"whsec_123\",\"rate_limit\":100,\"created_at\":\"2026-08-20T22:00:00Z\"}", System.Text.Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var client = new MiniSvixClient(httpClient, NullLogger<MiniSvixClient>.Instance);

        var endpoint = await client.CreateEndpointAsync("http://test.com/wh", "whsec_123", 100, TestContext.CancellationToken);

        Assert.AreEqual("ep_987", endpoint.Id);
        Assert.AreEqual("eshop", endpoint.TenantId);
        Assert.AreEqual("http://test.com/wh", endpoint.Url);

        var request = handler.Requests[0];
        Assert.AreEqual(HttpMethod.Post, request.Method);
        Assert.AreEqual("/api/v1/endpoints", request.RequestUri!.AbsolutePath);
        Assert.AreEqual("eshop", request.Headers.GetValues("X-Tenant-ID").Single());
    }

    [TestMethod]
    public async Task MiniSvixClientRespectsCustomTenantOptions()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("{\"id\":\"evt_custom\",\"status\":\"accepted\",\"created_at\":\"2026-08-20T22:00:00Z\"}", System.Text.Encoding.UTF8, "application/json")
        });

        var options = Microsoft.Extensions.Options.Options.Create(new MiniSvixOptions { TenantId = "custom_tenant_xyz" });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var client = new MiniSvixClient(httpClient, NullLogger<MiniSvixClient>.Instance, options);

        await client.IngestEventAsync("order.paid", new { }, Guid.NewGuid(), TestContext.CancellationToken);

        var request = handler.Requests[0];
        Assert.AreEqual("custom_tenant_xyz", request.Headers.GetValues("X-Tenant-ID").Single());
    }

    private sealed class RecordingHandler(HttpResponseMessage? cannedResponse = null) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(cannedResponse ?? new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
