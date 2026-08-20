internal static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.AddDefaultAuthentication();

        builder.AddRabbitMqEventBus("eventbus")
               .AddEventBusSubscriptions();

        builder.AddNpgsqlDbContext<WebhooksContext>("webhooksdb");

        builder.Services.AddMigration<WebhooksContext>();

        builder.Services.Configure<MiniSvixOptions>(builder.Configuration.GetSection("MiniSvix"));

        builder.Services.AddHttpClient<IMiniSvixClient, MiniSvixClient>(client =>
        {
            var url = builder.Configuration["MiniSvix:Url"] 
                      ?? builder.Configuration["services:minisvix-engine:http:0"] 
                      ?? "http://localhost:8080";
            client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        builder.Services.AddTransient<IGrantUrlTesterService, GrantUrlTesterService>();
        builder.Services.AddTransient<IWebhooksRetriever, WebhooksRetriever>();
        builder.Services.AddTransient<IWebhooksSender, WebhooksSender>();
    }

    private static void AddEventBusSubscriptions(this IEventBusBuilder eventBus)
    {
        eventBus.AddSubscription<ProductPriceChangedIntegrationEvent, ProductPriceChangedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToShippedIntegrationEvent, OrderStatusChangedToShippedIntegrationEventHandler>();
        eventBus.AddSubscription<OrderStatusChangedToPaidIntegrationEvent, OrderStatusChangedToPaidIntegrationEventHandler>();
    }
}
