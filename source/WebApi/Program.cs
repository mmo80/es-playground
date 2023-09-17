using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
using Weasel.Core;
using Marten;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Health;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

// This is the absolute, simplest way to integrate Marten into your
// .NET application with Marten's default configuration
builder.Services.AddMarten(options =>
{
    // Establish the connection string to your Marten database
    options.Connection(builder.Configuration.GetConnectionString("DefaultConnection")!);

    // If we're running in development mode, let Marten just take care
    // of all necessary schema building and patching behind the scenes
    if (builder.Environment.IsDevelopment())
    {
        options.AutoCreateSchemaObjects = AutoCreate.All;
    }
}).OptimizeArtifactWorkflow();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapHealthChecks("/_health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});


app.MapGet("/heartbeat", () => "Alive!");

app.MapGet("/hello", (string name) => $"Hello {name}");


// You can inject the IDocumentStore and open sessions yourself
app.MapPost("/users",
    async ([FromServices] IDocumentStore store, UserModel create) =>
    {
        // Open a session for querying, loading, and updating documents
        await using var session = store.LightweightSession();

        var user = new UserMarten {
            Username = create.Username
        };
        session.Store(user);

        await session.SaveChangesAsync();
    });

app.MapGet("/users", async ([FromServices] IDocumentStore store) =>
    {
        // Open a session for querying documents only
        await using var session = store.QuerySession();

        return await session.Query<UserMarten>()
            .ToListAsync();
    });

// OR Inject the session directly to skip the management of the session lifetime
app.MapGet("/user/{id:guid}",
    async (Guid id, [FromServices] IQuerySession session, CancellationToken ct) =>
    {
        return await session.LoadAsync<UserMarten>(id, ct);
    });

app.Run();


public class UserMarten
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
}


public record UserModel(string Username);
