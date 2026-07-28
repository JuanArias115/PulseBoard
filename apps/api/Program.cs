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

var inMemoryDatabaseName = $"PulseBoard-{Guid.NewGuid():N}";

builder.Services.AddDbContext<PulseBoardDbContext>(options =>
{
    if (builder.Configuration.GetValue<bool>("PulseBoard:UseInMemory"))
    {
        options.UseInMemoryDatabase(inMemoryDatabaseName);
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

api.MapGet("/profile", (IConfiguration configuration) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();

    return Results.Ok(new
    {
        userId = "local-dev",
        displayName = "Juan",
        preferredLanguage = options.DefaultLanguage,
        supportedLanguages = GetSupportedLanguages(options),
        timeZoneId = options.TimeZoneId,
        units = new
        {
            weight = "kg",
            distance = "km",
            height = "cm",
            energy = "kcal",
            bodyComposition = "%"
        }
    });
});

api.MapGet("/meta", (IConfiguration configuration) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();

    return Results.Ok(new
    {
        name = "PulseBoard",
        status = "online",
        defaultLanguage = options.DefaultLanguage,
        supportedLanguages = GetSupportedLanguages(options),
        timeZoneId = options.TimeZoneId,
        units = "metric",
        modules = new[] { "check-in", "habits", "body-measurements", "nutrition", "activity" }
    });
});

api.MapGet("/check-ins", async (PulseBoardDbContext db, int limit = 14) =>
{
    var safeLimit = Math.Clamp(limit, 1, 90);

    var checkIns = await db.CheckIns
        .OrderByDescending(checkIn => checkIn.LocalDate)
        .Take(safeLimit)
        .ToListAsync();

    return Results.Ok(checkIns);
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

api.MapGet("/habits", async (PulseBoardDbContext db) =>
{
    var habits = await db.Habits
        .Where(habit => habit.IsActive)
        .OrderBy(habit => habit.Name)
        .ToListAsync();

    return Results.Ok(habits);
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

api.MapGet("/habit-completions", async (PulseBoardDbContext db, string? localDate = null) =>
{
    var date = string.IsNullOrWhiteSpace(localDate)
        ? DateOnly.FromDateTime(DateTime.UtcNow).ToString("O")
        : localDate;

    if (!DateOnly.TryParse(date, out _))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(localDate)] = ["localDate must be a valid date."]
        });
    }

    var completions = await db.HabitCompletions
        .Where(completion => completion.LocalDate == date)
        .OrderByDescending(completion => completion.CompletedAtUtc)
        .ToListAsync();

    return Results.Ok(completions);
});

api.MapPost("/habits/{habitId:guid}/completions", async (
    Guid habitId,
    CreateHabitCompletionRequest request,
    PulseBoardDbContext db) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var habitExists = await db.Habits.AnyAsync(habit => habit.Id == habitId && habit.IsActive);
    if (!habitExists)
    {
        return Results.NotFound();
    }

    var existing = await db.HabitCompletions
        .FirstOrDefaultAsync(completion => completion.HabitId == habitId && completion.LocalDate == request.LocalDate);

    if (existing is not null)
    {
        return Results.Ok(existing);
    }

    var completion = request.ToEntity(habitId);
    db.HabitCompletions.Add(completion);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/habit-completions/{completion.Id}", completion);
});

api.MapGet("/body-measurements", async (PulseBoardDbContext db, int limit = 30) =>
{
    var safeLimit = Math.Clamp(limit, 1, 180);

    var measurements = await db.BodyMeasurements
        .OrderByDescending(measurement => measurement.MeasuredAtUtc)
        .Take(safeLimit)
        .ToListAsync();

    return Results.Ok(measurements);
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

api.MapPost("/integrations/apple-health/body-measurements", async (
    CreateAppleHealthBodyMeasurementRequest request,
    HttpRequest httpRequest,
    IConfiguration configuration,
    PulseBoardDbContext db) =>
{
    var bridgeKey = configuration.GetValue<string>("PULSEBOARD_APPLE_HEALTH_BRIDGE_KEY");
    if (string.IsNullOrWhiteSpace(bridgeKey))
    {
        return Results.Problem("Apple Health bridge key is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!httpRequest.Headers.TryGetValue("X-PulseBoard-Bridge-Key", out var providedKey)
        || providedKey.Count != 1
        || !string.Equals(providedKey[0], bridgeKey, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var bodyRequest = request.ToBodyMeasurementRequest();
    var errors = bodyRequest.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var measurement = bodyRequest.ToEntity("AppleHealth");
    db.BodyMeasurements.Add(measurement);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/body-measurements/{measurement.Id}", measurement);
});

if (app.Configuration.GetValue<bool>("PULSEBOARD_AUTO_MIGRATE"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PulseBoardDbContext>();
    await EnsureDatabaseAsync(db);
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

static string[] GetSupportedLanguages(PulseBoardOptions options)
    => options.SupportedLanguages
        .Where(language => !string.IsNullOrWhiteSpace(language))
        .Select(language => language.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

static async Task EnsureDatabaseAsync(PulseBoardDbContext db)
{
    await db.Database.EnsureCreatedAsync();

    if (!db.Database.IsRelational())
    {
        return;
    }

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "HabitCompletions" (
            "Id" uuid NOT NULL,
            "HabitId" uuid NOT NULL,
            "UserId" character varying(80) NOT NULL,
            "LocalDate" character varying(10) NOT NULL,
            "TimeZoneId" character varying(80) NOT NULL,
            "Amount" numeric(8,2) NULL,
            "Notes" character varying(1000) NULL,
            "CompletedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_HabitCompletions" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_HabitCompletions_Habits_HabitId" FOREIGN KEY ("HabitId") REFERENCES "Habits" ("Id") ON DELETE CASCADE
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_HabitCompletions_UserId_HabitId_LocalDate"
            ON "HabitCompletions" ("UserId", "HabitId", "LocalDate");
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_HabitCompletions_HabitId"
            ON "HabitCompletions" ("HabitId");
        """);
}

public partial class Program;
