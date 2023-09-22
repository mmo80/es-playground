using HealthChecks.UI.Client;
using Marten;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Marten.Schema.Identity;
using Marten.Services.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Weasel.Core;
using WebApi;
using WebApi.Event;
using WebApi.Health;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(rb => 
        rb
            .AddService(DiagnosticsConfig.ServiceName)
            .AddAttributes(new List<KeyValuePair<string, object>>
            {
                new("ES","Playground")
            })
    )
    .WithMetrics(m =>
    {
        m
            .AddMeter(DiagnosticsConfig.Meter.Name)
            .AddOtlpExporter();
    })
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource(DiagnosticsConfig.ActivitySource.Name)
            .ConfigureResource(resource => resource
                .AddService(DiagnosticsConfig.ServiceName))
            .AddNpgsql()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://collector:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    });

builder.Services.AddLogging(l =>
{
    l.AddOpenTelemetry(o =>
    {
        o.SetResourceBuilder(
                ResourceBuilder
                    .CreateDefault()
                    .AddService(DiagnosticsConfig.ServiceName))
        .AddOtlpExporter();
    });
});

// This is the absolute, simplest way to integrate Marten into your
// .NET application with Marten's default configuration
builder.Services.AddMarten(options =>
{
    // Establish the connection string to your Marten database
    options.Connection(builder.Configuration.GetConnectionString("DefaultConnection")!);

    options.UseDefaultSerialization(
        EnumStorage.AsString,
        nonPublicMembersStorage: NonPublicMembersStorage.All,
        serializerType: SerializerType.SystemTextJson
    );

    //// If we're running in development mode, let Marten just take care
    //// of all necessary schema building and patching behind the scenes
    //if (builder.Environment.IsDevelopment())
    //{
    //    options.AutoCreateSchemaObjects = AutoCreate.All;
    //}

    options.AutoCreateSchemaObjects = AutoCreate.All;

    options.Projections.Add<OrderModelProjection>(ProjectionLifecycle.Inline);
})
    //.OptimizeArtifactWorkflow()
    .UseLightweightSessions()
    .AddAsyncDaemon(DaemonMode.Solo);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapHealthChecks("/_health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});


app.MapGet("/alive", () => "Alive!");

// You can inject the IDocumentStore and open sessions yourself
app.MapPost("/users",
    async ([FromServices] IDocumentStore store, UserRequest create) =>
    {
        // Open a session for querying, loading, and updating documents
        await using var session = store.LightweightSession();

        var user = new UserMarten {
            Username = create.Username
        };
        session.Store(user);

        await session.SaveChangesAsync();

        DiagnosticsConfig.AddCustomerMetrics();
    });

app.MapGet("/users", async ([FromServices] IDocumentStore store) =>
    {
        // Open a session for querying documents only
        await using var session = store.QuerySession();

        return await session.Query<UserMarten>()
            .ToListAsync();
    });

// OR Inject the session directly to skip the management of the session lifetime
app.MapGet("/users/{id:guid}",
    async (Guid id, [FromServices] IQuerySession session, CancellationToken ct) =>
    {
        return await session.LoadAsync<UserMarten>(id, ct);
    });


app.MapPost("/order/create",
    async ([FromServices] IDocumentStore store, decimal total) =>
    {
        using var activity = DiagnosticsConfig.ActivitySource
            .StartActivity(DiagnosticsNames.CreateOrderMarkupName);

        await using var session = store.LightweightSession();

        var orderId = CombGuidIdGeneration.NewGuid();
        session.Events.StartStream<Order>(orderId, new OrderCreated(orderId, total, DateTimeOffset.Now));
        await session.SaveChangesAsync();

        activity?.SetTag(DiagnosticsNames.OrderId, orderId.ToString());

        DiagnosticsConfig.AddOrderMetrics();

        return Results.Created($"/order/{orderId}", orderId);
    });

app.MapPost("/order/{orderId:guid}/complete-payment",
    async ([FromServices] IDocumentSession session, Guid orderId, int version) =>
    {
        var aggregate = await session.Events.AggregateStreamAsync<Order>(orderId) 
            ?? throw new InvalidOperationException("Order does not exist");

        aggregate.CompletePayment();

        await session.Events.WriteToAggregate<Order>(orderId, version, stream =>
            stream.AppendMany(aggregate.GetUncommittedEvents()));

        await session.SaveChangesAsync();
    });

app.MapPost("/order/{orderId:guid}/ship",
    async ([FromServices] IDocumentSession session, Guid orderId, int version) =>
    {
        var aggregate = await session.Events.AggregateStreamAsync<Order>(orderId) 
            ?? throw new InvalidOperationException("Order does not exist");

        aggregate.Ship();

        await session.Events.WriteToAggregate<Order>(orderId, version, stream =>
            stream.AppendMany(aggregate.GetUncommittedEvents()));

        await session.SaveChangesAsync();
    });

app.MapPost("/order/{orderId:guid}/cancel",
    async ([FromServices] IDocumentSession session, Guid orderId, int version) =>
    {
        var aggregate = await session.Events.AggregateStreamAsync<Order>(orderId) 
            ?? throw new InvalidOperationException("Order does not exist");

        aggregate.Cancel();

        await session.Events.WriteToAggregate<Order>(orderId, version, stream =>
            stream.AppendMany(aggregate.GetUncommittedEvents()));

        await session.SaveChangesAsync();
    });

app.MapGet("/order/{id:guid}",
    async (Guid id, [FromServices] IQuerySession session, CancellationToken ct) 
        => await session.Json.FindByIdAsync<OrderModel>(id, ct));

app.Run();


public class UserMarten
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
}

public record UserRequest(string Username);
