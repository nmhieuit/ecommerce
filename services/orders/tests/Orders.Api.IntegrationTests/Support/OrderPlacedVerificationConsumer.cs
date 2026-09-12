using System.Collections.Concurrent;
using EventContracts;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Api.IntegrationTests.Support;

/// <summary>
/// A consumer that exists only to verify the outbox/inbox mechanism (024-verify-transactional-outbox
/// research.md Decision 3) — not a business capability. Records how many times its own body actually
/// ran for a given <see cref="OrderPlacedV1.EventId"/>; a real consumer's idempotency is proven by
/// this count staying at 1 even when the same message is delivered twice, via MassTransit's real
/// <c>InboxState</c> deduplication (<see cref="VerificationDbContext"/>), not hand-rolled dedupe
/// logic here.
/// </summary>
public sealed class OrderPlacedVerificationConsumer : IConsumer<OrderPlacedV1>
{
    public static readonly ConcurrentDictionary<Guid, int> ProcessedCounts = new();

    public Task Consume(ConsumeContext<OrderPlacedV1> context)
    {
        ProcessedCounts.AddOrUpdate(context.Message.EventId, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Homes only <c>InboxState</c> (data-model.md) — this consumer publishes nothing of its own, so it
/// has no need for the <c>OutboxMessage</c>/<c>OutboxState</c> tables <see cref="OrdersDbContext"/>
/// carries for the producer side.
/// </summary>
public sealed class VerificationDbContext(DbContextOptions<VerificationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
    }
}

/// <summary>
/// A standalone MassTransit bus — deliberately not part of <c>Orders.Api</c>'s own DI container
/// (which is already fully composed by the time a test's <c>WebApplicationFactory</c> runs) — that
/// consumes <see cref="OrderPlacedV1"/> from the same RabbitMQ broker <c>Orders.Api</c> publishes to.
/// Stands in for a genuine, independent downstream consumer service (research.md Decision 3).
/// </summary>
public sealed class VerificationConsumerHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly IBusControl _busControl;

    private VerificationConsumerHost(ServiceProvider services, IBusControl busControl)
    {
        _services = services;
        _busControl = busControl;
    }

    public static async Task<VerificationConsumerHost> StartAsync(
        string sqlConnectionString,
        string rabbitMqConnectionString)
    {
        var services = new ServiceCollection();

        services.AddDbContext<VerificationDbContext>(options => options.UseSqlServer(sqlConnectionString));

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<VerificationDbContext>(o => o.UseSqlServer());
            x.AddConsumer<OrderPlacedVerificationConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConnectionString));

                cfg.ReceiveEndpoint("order-placed-verification", e =>
                {
                    e.UseEntityFrameworkOutbox<VerificationDbContext>(context);
                    e.ConfigureConsumer<OrderPlacedVerificationConsumer>(context);
                });
            });
        });

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<VerificationDbContext>()
                .Database.EnsureCreatedAsync();
        }

        var busControl = provider.GetRequiredService<IBusControl>();
        await busControl.StartAsync();

        return new VerificationConsumerHost(provider, busControl);
    }

    public async ValueTask DisposeAsync()
    {
        await _busControl.StopAsync();
        await _services.DisposeAsync();
    }
}
