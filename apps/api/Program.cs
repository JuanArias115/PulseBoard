using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using PulseBoard.Api;
using PulseBoard.Api.Data;
using PulseBoard.Api.Health;
using PulseBoard.Api.Requests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.Configure<PulseBoardOptions>(
    builder.Configuration.GetSection(PulseBoardOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var origins = builder.Configuration.GetValue<string>("PULSEBOARD_CORS_ORIGINS")
            ?? "http://localhost:4200,https://pulseboard.juanariasdev.com";

        policy
            .WithOrigins(origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<PulseBoardDbContext>(options =>
{
    if (builder.Configuration.GetValue<bool>("PulseBoard:UseInMemory"))
    {
        options.UseInMemoryDatabase($"PulseBoard-{Guid.NewGuid():N}");
        return;
    }

    options.UseNpgsql(GetConnectionString(builder.Configuration));
});

builder.Services
    .AddHealthChecks()
    .AddCheck<SelfHealthCheck>("self", tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors("web");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready") || registration.Tags.Contains("live")
});

var api = app.MapGroup("/api/v1");

api.MapGet("/meta", (IConfiguration configuration) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();

    return Results.Ok(new
    {
        name = "PulseBoard",
        status = "online",
        defaultLanguage = options.DefaultLanguage,
        supportedLanguages = options.SupportedLanguages,
        timeZoneId = options.TimeZoneId,
        units = "metric",
        modules = new[] { "check-in", "habits", "body-measurements", "nutrition", "activity" }
    });
});

api.MapPost("/check-ins", async (CreateCheckInRequest request, PulseBoardDbContext db) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var checkIn = request.ToEntity();
    db.CheckIns.Add(checkIn);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/check-ins/{checkIn.Id}", checkIn);
});

api.MapPost("/habits", async (CreateHabitRequest request, PulseBoardDbContext db) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var habit = request.ToEntity();
    db.Habits.Add(habit);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/habits/{habit.Id}", habit);
});

api.MapPost("/body-measurements", async (CreateBodyMeasurementRequest request, PulseBoardDbContext db) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var measurement = request.ToEntity();
    db.BodyMeasurements.Add(measurement);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/body-measurements/{measurement.Id}", measurement);
});

if (app.Configuration.GetValue<bool>("PULSEBOARD_AUTO_MIGRATE"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PulseBoardDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();

static string GetConnectionString(IConfiguration configuration)
{
    var configured = configuration.GetConnectionString("Default");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured;
    }

    var host = GetRequiredConnectionSetting(configuration, "PULSEBOARD_POSTGRES_HOST");
    var port = configuration.GetValue<string>("PULSEBOARD_POSTGRES_PORT") ?? "5432";
    var database = GetRequiredConnectionSetting(configuration, "PULSEBOARD_POSTGRES_DB");
    var user = GetRequiredConnectionSetting(configuration, "PULSEBOARD_POSTGRES_USER");
    var password = GetRequiredConnectionSetting(configuration, "PULSEBOARD_POSTGRES_PASSWORD");

    return $"Host={host};Port={port};Database={database};Username={user};Password={password}";
}

static string GetRequiredConnectionSetting(IConfiguration configuration, string key)
{
    var value = configuration.GetValue<string>(key);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{key} must be configured.");
    }

    return value;
}

public partial class Program;
