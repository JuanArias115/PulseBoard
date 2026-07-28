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

api.MapGet("/dashboard", async (PulseBoardDbContext db, IConfiguration configuration) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();
    var nowUtc = DateTimeOffset.UtcNow;
    var localToday = GetLocalDate(nowUtc, options.TimeZoneId);
    var recentDates = Enumerable.Range(0, 7)
        .Select(offset => localToday.AddDays(-offset).ToString("O"))
        .ToArray();

    var checkIns = await db.CheckIns
        .OrderByDescending(checkIn => checkIn.LocalDate)
        .Take(14)
        .ToListAsync();

    var habits = await db.Habits
        .Where(habit => habit.IsActive)
        .OrderBy(habit => habit.Name)
        .ToListAsync();

    var completions = await db.HabitCompletions
        .Where(completion => recentDates.Contains(completion.LocalDate))
        .ToListAsync();

    var measurements = await db.BodyMeasurements
        .Where(measurement => measurement.MeasuredAtUtc >= nowUtc.AddDays(-90))
        .OrderByDescending(measurement => measurement.MeasuredAtUtc)
        .Take(120)
        .ToListAsync();

    var todayCheckIn = checkIns.FirstOrDefault(checkIn => checkIn.LocalDate == localToday.ToString("O"));
    var latestCheckIn = todayCheckIn ?? checkIns.FirstOrDefault();
    var latestMeasurement = measurements.FirstOrDefault();
    var completionsToday = completions.Count(completion => completion.LocalDate == localToday.ToString("O"));
    var habitsExpectedLast7Days = habits.Count * recentDates.Length;
    var completionRate = habitsExpectedLast7Days == 0
        ? 0
        : Math.Round((decimal)completions.Count / habitsExpectedLast7Days * 100, 0);

    var bodyTrends = new[]
    {
        BuildTrend("weight", "Peso", "Weight", "kg", measurements, measurement => measurement.WeightKg, nowUtc),
        BuildTrend("bodyFat", "Grasa", "Body fat", "%", measurements, measurement => measurement.BodyFatPercentage, nowUtc),
        BuildTrend("muscle", "Musculo", "Muscle", "%", measurements, measurement => measurement.MusclePercentage, nowUtc),
        BuildTrend("water", "Agua", "Water", "%", measurements, measurement => measurement.BodyWaterPercentage, nowUtc)
    };

    var readinessScore = BuildReadinessScore(latestCheckIn, habits.Count, completionsToday);
    var insights = BuildInsights(checkIns, measurements, completionRate, bodyTrends);

    return Results.Ok(new DashboardResponse(
        GeneratedAtUtc: nowUtc,
        LocalDate: localToday.ToString("O"),
        TimeZoneId: options.TimeZoneId,
        ReadinessScore: readinessScore,
        Today: new TodaySummary(
            SleepHours: latestCheckIn?.SleepHours,
            Energy: latestCheckIn?.Energy,
            Recovery: latestCheckIn?.Recovery,
            CompletedHabits: completionsToday,
            TotalHabits: habits.Count),
        Habits: new HabitSummary(
            Active: habits.Count,
            CompletedToday: completionsToday,
            CompletionRate7Days: completionRate,
            StreakDays: BuildHabitStreak(recentDates, habits.Count, completions)),
        Body: new BodyDashboard(
            Latest: latestMeasurement,
            Trends: bodyTrends,
            History: measurements
                .OrderBy(measurement => measurement.MeasuredAtUtc)
                .TakeLast(30)
                .Select(measurement => new BodyHistoryPoint(
                    LocalDate: GetLocalDate(measurement.MeasuredAtUtc, options.TimeZoneId).ToString("O"),
                    WeightKg: measurement.WeightKg,
                    BodyFatPercentage: measurement.BodyFatPercentage,
                    MusclePercentage: measurement.MusclePercentage,
                    BodyWaterPercentage: measurement.BodyWaterPercentage))
                .ToArray()),
        Insights: insights));
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

static TrendMetric BuildTrend(
    string key,
    string labelEs,
    string labelEn,
    string unit,
    IReadOnlyCollection<PulseBoard.Api.Models.BodyMeasurement> measurements,
    Func<PulseBoard.Api.Models.BodyMeasurement, decimal?> selector,
    DateTimeOffset nowUtc)
{
    var points = measurements
        .Select(measurement => new MeasurementPoint(measurement.MeasuredAtUtc, selector(measurement)))
        .Where(point => point.Value is not null)
        .OrderByDescending(point => point.MeasuredAtUtc)
        .ToArray();

    var latest = points.FirstOrDefault()?.Value;
    var average7 = AverageSince(points, nowUtc.AddDays(-7));
    var average14 = AverageSince(points, nowUtc.AddDays(-14));
    var average30 = AverageSince(points, nowUtc.AddDays(-30));
    var oldest30 = points
        .Where(point => point.MeasuredAtUtc >= nowUtc.AddDays(-30))
        .OrderBy(point => point.MeasuredAtUtc)
        .FirstOrDefault()
        ?.Value;
    decimal? latestRounded = latest.HasValue ? Round(latest.Value) : null;
    decimal? change30 = latest.HasValue && oldest30.HasValue ? Round(latest.Value - oldest30.Value) : null;
    var recentPointCount = points.Count(point => point.MeasuredAtUtc >= nowUtc.AddDays(-30));
    var trend = ResolveTrend(change30, unit, recentPointCount);

    return new TrendMetric(
        Key: key,
        LabelEs: labelEs,
        LabelEn: labelEn,
        Unit: unit,
        Latest: latestRounded,
        Average7: average7,
        Average14: average14,
        Average30: average30,
        Change30: change30,
        Trend: trend.Code,
        TrendEs: trend.Es,
        TrendEn: trend.En,
        DataPoints: points.Length);
}

static decimal? AverageSince(IEnumerable<MeasurementPoint> points, DateTimeOffset sinceUtc)
{
    var values = points
        .Where(point => point.MeasuredAtUtc >= sinceUtc)
        .Select(point => point.Value!.Value)
        .ToArray();

    return values.Length == 0 ? null : Round(values.Average());
}

static (string Code, string Es, string En) ResolveTrend(decimal? change, string unit, int recentPoints)
{
    if (recentPoints < 3 || change is null)
    {
        return ("insufficient", "Informacion insuficiente", "Insufficient data");
    }

    var threshold = unit == "kg" ? 0.3m : 0.5m;
    if (Math.Abs(change.Value) <= threshold)
    {
        return ("stable", "Estable", "Stable");
    }

    return change.Value < 0
        ? ("down", "Bajando gradualmente", "Gradually decreasing")
        : ("up", "Subiendo gradualmente", "Gradually increasing");
}

static decimal Round(decimal value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

static int BuildReadinessScore(PulseBoard.Api.Models.CheckIn? checkIn, int totalHabits, int completedHabits)
{
    if (checkIn is null)
    {
        return 0;
    }

    var habitScore = totalHabits == 0 ? 75 : (int)Math.Round((decimal)completedHabits / totalHabits * 100, 0);
    var checkInScore = (checkIn.Energy + checkIn.Mood + checkIn.Recovery + (6 - checkIn.Fatigue)) / 4m * 20m;
    var sleepScore = Math.Clamp(checkIn.SleepHours / 8m * 100m, 0m, 100m);

    return (int)Math.Round(checkInScore * 0.45m + sleepScore * 0.3m + habitScore * 0.25m, 0);
}

static int BuildHabitStreak(string[] recentDates, int totalHabits, IReadOnlyCollection<PulseBoard.Api.Models.HabitCompletion> completions)
{
    if (totalHabits == 0)
    {
        return 0;
    }

    var streak = 0;
    foreach (var date in recentDates)
    {
        var completedForDate = completions
            .Where(completion => completion.LocalDate == date)
            .Select(completion => completion.HabitId)
            .Distinct()
            .Count();

        if (completedForDate == 0)
        {
            break;
        }

        streak++;
    }

    return streak;
}

static Insight[] BuildInsights(
    IReadOnlyList<PulseBoard.Api.Models.CheckIn> checkIns,
    IReadOnlyCollection<PulseBoard.Api.Models.BodyMeasurement> measurements,
    decimal completionRate,
    IReadOnlyList<TrendMetric> bodyTrends)
{
    var insights = new List<Insight>();
    var weightTrend = bodyTrends.FirstOrDefault(trend => trend.Key == "weight");

    if (weightTrend is null || weightTrend.Trend == "insufficient")
    {
        insights.Add(new Insight(
            Category: "data",
            Severity: "info",
            MessageEs: "Aun faltan mediciones corporales para calcular tendencias confiables de 7, 14 y 30 dias.",
            MessageEn: "More body measurements are needed before 7, 14, and 30 day trends are reliable."));
    }
    else
    {
        insights.Add(new Insight(
            Category: "body",
            Severity: "info",
            MessageEs: $"La tendencia de peso de 30 dias esta: {weightTrend.TrendEs.ToLowerInvariant()}.",
            MessageEn: $"The 30 day weight trend is: {weightTrend.TrendEn.ToLowerInvariant()}."));
    }

    if (completionRate >= 80)
    {
        insights.Add(new Insight("habits", "positive", "Tu constancia de habitos de 7 dias esta alta.", "Your 7 day habit consistency is high."));
    }
    else if (completionRate > 0)
    {
        insights.Add(new Insight("habits", "info", "Hay margen para subir la constancia semanal de habitos.", "There is room to improve weekly habit consistency."));
    }

    if (checkIns.Count >= 3)
    {
        var averageSleep = checkIns.Take(7).Average(checkIn => checkIn.SleepHours);
        var averageEnergy = checkIns.Take(7).Average(checkIn => checkIn.Energy);
        if (averageSleep < 7 && averageEnergy <= 3)
        {
            insights.Add(new Insight(
                Category: "recovery",
                Severity: "warning",
                MessageEs: "El sueno medio reciente esta por debajo de 7 horas y la energia no esta alta.",
                MessageEn: "Recent average sleep is below 7 hours and energy is not high."));
        }
    }

    if (measurements.Count == 0 && checkIns.Count == 0)
    {
        insights.Add(new Insight("data", "info", "Empieza registrando un check-in y una medicion Soehnle.", "Start by logging one check-in and one Soehnle measurement."));
    }

    return insights.Take(4).ToArray();
}

static DateOnly GetLocalDate(DateTimeOffset utcDateTime, string timeZoneId)
{
    TimeZoneInfo timeZone;
    try
    {
        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
    catch (TimeZoneNotFoundException)
    {
        timeZone = TimeZoneInfo.Utc;
    }

    return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcDateTime, timeZone).DateTime);
}

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

public sealed record DashboardResponse(
    DateTimeOffset GeneratedAtUtc,
    string LocalDate,
    string TimeZoneId,
    int ReadinessScore,
    TodaySummary Today,
    HabitSummary Habits,
    BodyDashboard Body,
    Insight[] Insights);

public sealed record TodaySummary(
    decimal? SleepHours,
    int? Energy,
    int? Recovery,
    int CompletedHabits,
    int TotalHabits);

public sealed record HabitSummary(
    int Active,
    int CompletedToday,
    decimal CompletionRate7Days,
    int StreakDays);

public sealed record BodyDashboard(
    PulseBoard.Api.Models.BodyMeasurement? Latest,
    TrendMetric[] Trends,
    BodyHistoryPoint[] History);

public sealed record TrendMetric(
    string Key,
    string LabelEs,
    string LabelEn,
    string Unit,
    decimal? Latest,
    decimal? Average7,
    decimal? Average14,
    decimal? Average30,
    decimal? Change30,
    string Trend,
    string TrendEs,
    string TrendEn,
    int DataPoints);

public sealed record BodyHistoryPoint(
    string LocalDate,
    decimal WeightKg,
    decimal? BodyFatPercentage,
    decimal? MusclePercentage,
    decimal? BodyWaterPercentage);

public sealed record Insight(
    string Category,
    string Severity,
    string MessageEs,
    string MessageEn);

public sealed record MeasurementPoint(DateTimeOffset MeasuredAtUtc, decimal? Value);
