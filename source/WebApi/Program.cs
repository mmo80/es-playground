using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
#pragma warning disable CS8618

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<DbApiContext>
    (options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapGet("/heartbeat", () => "Alive!");

app.MapGet("/hello", (string name) => $"Hello {name}");

app.MapGet("/users", async (DbApiContext db) => await db.Users.ToListAsync());

app.MapGet("/users/{id}", async (Guid id, DbApiContext db) =>
    await db.Users.FindAsync(id)
        is { } user
        ? Results.Ok(user)
        : Results.NotFound());

app.MapPost("/users", async (DbApiContext db, UserModel user) =>
{
    var u = new User { Id = Guid.NewGuid(), Username = user.Username };

    db.Users.Add(u);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{u.Id}", u);
});

app.Run();




public record UserModel(string Username);

[Table("users")]
public class User
{
    [System.ComponentModel.DataAnnotations.Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("username")]
    public string Username { get; set; }
}


public class DbApiContext : DbContext
{
    public DbApiContext(DbContextOptions options) : base(options)
    { }

    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {}
}