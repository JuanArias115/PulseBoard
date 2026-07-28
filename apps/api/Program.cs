using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    var meals = await db.Meals
        .Where(meal => recentDates.Contains(meal.LocalDate))
        .OrderByDescending(meal => meal.EatenAtUtc)
        .ToListAsync();

    var dailyActivities = await db.DailyActivities
        .Where(activity => recentDates.Contains(activity.LocalDate))
        .OrderByDescending(activity => activity.LocalDate)
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
        Nutrition: BuildNutritionSummary(localToday.ToString("O"), meals),
        Activity: BuildActivitySummary(localToday.ToString("O"), dailyActivities),
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
        Insights: BuildNutritionInsights(localToday.ToString("O"), insights, meals)));
});

api.MapGet("/analysis", async (PulseBoardDbContext db, IConfiguration configuration) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();
    var nowUtc = DateTimeOffset.UtcNow;
    var localToday = GetLocalDate(nowUtc, options.TimeZoneId);
    var recentDates7 = Enumerable.Range(0, 7)
        .Select(offset => localToday.AddDays(-offset).ToString("O"))
        .ToArray();
    var recentDates14 = Enumerable.Range(0, 14)
        .Select(offset => localToday.AddDays(-offset).ToString("O"))
        .ToArray();

    var checkIns = await db.CheckIns
        .Where(checkIn => recentDates14.Contains(checkIn.LocalDate))
        .OrderByDescending(checkIn => checkIn.LocalDate)
        .ToListAsync();

    var habits = await db.Habits
        .Where(habit => habit.IsActive)
        .OrderBy(habit => habit.Name)
        .ToListAsync();

    var completions = await db.HabitCompletions
        .Where(completion => recentDates7.Contains(completion.LocalDate))
        .ToListAsync();

    var meals = await db.Meals
        .Where(meal => recentDates7.Contains(meal.LocalDate))
        .OrderByDescending(meal => meal.EatenAtUtc)
        .ToListAsync();

    var dailyActivities = await db.DailyActivities
        .Where(activity => recentDates7.Contains(activity.LocalDate))
        .OrderByDescending(activity => activity.LocalDate)
        .ToListAsync();

    var measurements = await db.BodyMeasurements
        .Where(measurement => measurement.MeasuredAtUtc >= nowUtc.AddDays(-90))
        .OrderByDescending(measurement => measurement.MeasuredAtUtc)
        .Take(120)
        .ToListAsync();

    var bodyTrends = new[]
    {
        BuildTrend("weight", "Peso", "Weight", "kg", measurements, measurement => measurement.WeightKg, nowUtc),
        BuildTrend("bodyFat", "Grasa", "Body fat", "%", measurements, measurement => measurement.BodyFatPercentage, nowUtc),
        BuildTrend("muscle", "Musculo", "Muscle", "%", measurements, measurement => measurement.MusclePercentage, nowUtc),
        BuildTrend("water", "Agua", "Water", "%", measurements, measurement => measurement.BodyWaterPercentage, nowUtc)
    };

    return Results.Ok(BuildAnalysisSummary(
        nowUtc,
        localToday.ToString("O"),
        options.TimeZoneId,
        recentDates7,
        checkIns,
        habits,
        completions,
        meals,
        dailyActivities,
        measurements,
        bodyTrends));
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

api.MapGet("/meals", async (PulseBoardDbContext db, string? localDate = null, int limit = 30) =>
{
    var safeLimit = Math.Clamp(limit, 1, 120);
    var query = db.Meals.AsQueryable();

    if (!string.IsNullOrWhiteSpace(localDate))
    {
        if (!DateOnly.TryParse(localDate, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(localDate)] = ["localDate must be a valid date."]
            });
        }

        query = query.Where(meal => meal.LocalDate == localDate);
    }

    var meals = await query
        .OrderByDescending(meal => meal.EatenAtUtc)
        .Take(safeLimit)
        .ToListAsync();

    return Results.Ok(meals);
});

api.MapGet("/meal-favorites", async (PulseBoardDbContext db) =>
{
    var meals = await db.Meals
        .Where(meal => meal.IsFavorite)
        .OrderByDescending(meal => meal.CreatedAtUtc)
        .Take(20)
        .ToListAsync();

    return Results.Ok(meals);
});

api.MapGet("/nutrition-summary", async (PulseBoardDbContext db, IConfiguration configuration, string? localDate = null) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();
    var date = string.IsNullOrWhiteSpace(localDate)
        ? GetLocalDate(DateTimeOffset.UtcNow, options.TimeZoneId).ToString("O")
        : localDate;

    if (!DateOnly.TryParse(date, out _))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(localDate)] = ["localDate must be a valid date."]
        });
    }

    var selectedDate = DateOnly.Parse(date);
    var recentDates = Enumerable.Range(0, 7)
        .Select(offset => selectedDate.AddDays(-offset).ToString("O"))
        .ToArray();

    var meals = await db.Meals
        .Where(meal => recentDates.Contains(meal.LocalDate))
        .OrderByDescending(meal => meal.EatenAtUtc)
        .ToListAsync();

    return Results.Ok(BuildNutritionSummary(date, meals));
});

api.MapPost("/meals", async (CreateMealRequest request, PulseBoardDbContext db) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var meal = request.ToEntity();
    db.Meals.Add(meal);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/meals/{meal.Id}", meal);
});

api.MapGet("/daily-activities", async (PulseBoardDbContext db, int limit = 30) =>
{
    var safeLimit = Math.Clamp(limit, 1, 180);

    var activities = await db.DailyActivities
        .OrderByDescending(activity => activity.LocalDate)
        .ThenByDescending(activity => activity.RecordedAtUtc)
        .Take(safeLimit)
        .ToListAsync();

    return Results.Ok(activities);
});

api.MapGet("/activity-summary", async (PulseBoardDbContext db, IConfiguration configuration, string? localDate = null) =>
{
    var options = configuration.GetSection(PulseBoardOptions.SectionName).Get<PulseBoardOptions>()
        ?? new PulseBoardOptions();
    var date = string.IsNullOrWhiteSpace(localDate)
        ? GetLocalDate(DateTimeOffset.UtcNow, options.TimeZoneId).ToString("O")
        : localDate;

    if (!DateOnly.TryParse(date, out _))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(localDate)] = ["localDate must be a valid date."]
        });
    }

    var selectedDate = DateOnly.Parse(date);
    var recentDates = Enumerable.Range(0, 7)
        .Select(offset => selectedDate.AddDays(-offset).ToString("O"))
        .ToArray();

    var activities = await db.DailyActivities
        .Where(activity => recentDates.Contains(activity.LocalDate))
        .OrderByDescending(activity => activity.LocalDate)
        .ToListAsync();

    return Results.Ok(BuildActivitySummary(date, activities));
});

api.MapPost("/daily-activities", async (CreateDailyActivityRequest request, PulseBoardDbContext db) =>
{
    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var activity = await UpsertDailyActivityAsync(db, request.ToEntity());

    return Results.Ok(activity);
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

api.MapPost("/integrations/apple-health/daily-activity", async (
    JsonElement payload,
    HttpRequest httpRequest,
    IConfiguration configuration,
    ILogger<Program> logger,
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

    logger.LogWarning("Temporary Apple Health daily activity payload: {Payload}", payload.GetRawText());

    var parsed = TryBuildDailyActivityRequest(payload, out var activityRequest, out var errors);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    if (!parsed || activityRequest is null)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["payload"] = ["Could not parse Apple Health activity payload."]
        });
    }

    var activity = await UpsertDailyActivityAsync(db, activityRequest.ToEntity("AppleHealth"));

    return Results.Ok(activity);
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

static NutritionSummary BuildNutritionSummary(string localDate, IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals)
{
    var todayMeals = meals
        .Where(meal => meal.LocalDate == localDate)
        .ToArray();
    var daysWithMeals = meals
        .Select(meal => meal.LocalDate)
        .Distinct()
        .Count();

    return new NutritionSummary(
        Today: BuildNutritionTotals(todayMeals),
        Average7Days: daysWithMeals == 0
            ? new NutritionTotals(0, 0, 0, 0, 0, 0)
            : new NutritionTotals(
                Meals: Math.Round((decimal)meals.Count / daysWithMeals, 1),
                CaloriesKcal: Math.Round((decimal)meals.Sum(meal => meal.CaloriesKcal) / daysWithMeals, 0),
                ProteinGrams: Round(meals.Sum(meal => meal.ProteinGrams) / daysWithMeals),
                CarbohydrateGrams: Round(meals.Sum(meal => meal.CarbohydrateGrams) / daysWithMeals),
                FatGrams: Round(meals.Sum(meal => meal.FatGrams) / daysWithMeals),
                VegetableMeals: Math.Round((decimal)meals.Count(meal => meal.HasVegetables) / daysWithMeals, 1)),
        LoggedDays7: daysWithMeals,
        LatestMeals: meals
            .OrderByDescending(meal => meal.EatenAtUtc)
            .Take(5)
            .ToArray());
}

static NutritionTotals BuildNutritionTotals(IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals) => new(
    Meals: meals.Count,
    CaloriesKcal: meals.Sum(meal => meal.CaloriesKcal),
    ProteinGrams: Round(meals.Sum(meal => meal.ProteinGrams)),
    CarbohydrateGrams: Round(meals.Sum(meal => meal.CarbohydrateGrams)),
    FatGrams: Round(meals.Sum(meal => meal.FatGrams)),
    VegetableMeals: meals.Count(meal => meal.HasVegetables));

static ActivitySummary BuildActivitySummary(string localDate, IReadOnlyCollection<PulseBoard.Api.Models.DailyActivity> activities)
{
    var todayActivities = activities
        .Where(activity => activity.LocalDate == localDate)
        .ToArray();
    var loggedDays = activities
        .Select(activity => activity.LocalDate)
        .Distinct()
        .Count();

    return new ActivitySummary(
        Today: todayActivities.Length == 0
            ? new ActivityTotals(0, 0, 0, 0, 0, 0)
            : BuildActivityTotals(todayActivities),
        Average7Days: loggedDays == 0
            ? new ActivityTotals(0, 0, 0, 0, 0, 0)
            : new ActivityTotals(
                Steps: Math.Round((decimal)activities.Sum(activity => activity.Steps) / loggedDays, 0),
                ActiveEnergyKcal: Math.Round((decimal)activities.Sum(activity => activity.ActiveEnergyKcal) / loggedDays, 0),
                ExerciseMinutes: Math.Round((decimal)activities.Sum(activity => activity.ExerciseMinutes) / loggedDays, 0),
                WalkingRunningDistanceKm: Round(activities.Sum(activity => activity.WalkingRunningDistanceKm ?? 0) / loggedDays),
                CyclingDistanceKm: Round(activities.Sum(activity => activity.CyclingDistanceKm ?? 0) / loggedDays),
                WorkoutCount: Math.Round((decimal)activities.Sum(activity => activity.WorkoutCount) / loggedDays, 1)),
        LoggedDays7: loggedDays,
        LatestActivities: activities
            .OrderByDescending(activity => activity.LocalDate)
            .ThenByDescending(activity => activity.RecordedAtUtc)
            .Take(7)
            .ToArray());
}

static ActivityTotals BuildActivityTotals(IReadOnlyCollection<PulseBoard.Api.Models.DailyActivity> activities) => new(
    Steps: activities.Sum(activity => activity.Steps),
    ActiveEnergyKcal: activities.Sum(activity => activity.ActiveEnergyKcal),
    ExerciseMinutes: activities.Sum(activity => activity.ExerciseMinutes),
    WalkingRunningDistanceKm: Round(activities.Sum(activity => activity.WalkingRunningDistanceKm ?? 0)),
    CyclingDistanceKm: Round(activities.Sum(activity => activity.CyclingDistanceKm ?? 0)),
    WorkoutCount: activities.Sum(activity => activity.WorkoutCount));

static Insight[] BuildNutritionInsights(string localDate, Insight[] currentInsights, IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals)
{
    var insights = currentInsights.ToList();
    var todayMeals = meals.Where(meal => meal.LocalDate == localDate).ToArray();

    if (todayMeals.Length == 0)
    {
        insights.Add(new Insight("nutrition", "info", "Aun no registraste comidas hoy.", "No meals have been logged today yet."));
    }
    else if (todayMeals.Sum(meal => meal.ProteinGrams) >= 100)
    {
        insights.Add(new Insight("nutrition", "positive", "La proteina registrada hoy ya es alta.", "Logged protein today is already high."));
    }

    if (todayMeals.Length > 0 && todayMeals.All(meal => !meal.HasVegetables))
    {
        insights.Add(new Insight("nutrition", "info", "Hoy no hay comidas marcadas con verduras.", "No meals today are marked with vegetables."));
    }

    return insights.Take(5).ToArray();
}

static AnalysisResponse BuildAnalysisSummary(
    DateTimeOffset generatedAtUtc,
    string localDate,
    string timeZoneId,
    string[] recentDates7,
    IReadOnlyList<PulseBoard.Api.Models.CheckIn> checkIns,
    IReadOnlyCollection<PulseBoard.Api.Models.Habit> habits,
    IReadOnlyCollection<PulseBoard.Api.Models.HabitCompletion> completions,
    IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals,
    IReadOnlyCollection<PulseBoard.Api.Models.DailyActivity> dailyActivities,
    IReadOnlyCollection<PulseBoard.Api.Models.BodyMeasurement> measurements,
    TrendMetric[] bodyTrends)
{
    var activity = BuildActivityComponent(dailyActivities);
    var recovery = BuildRecoveryComponent(checkIns);
    var nutrition = BuildNutritionComponent(meals);
    var consistency = BuildConsistencyComponent(recentDates7, habits, completions, checkIns);

    var bodyData = BuildBodyDataSignal(measurements, bodyTrends);
    var completeness = BuildDataCompleteness(checkIns, habits, completions, meals, dailyActivities, measurements);
    var observations = BuildAnalysisObservations(
        checkIns,
        habits,
        completions,
        meals,
        dailyActivities,
        bodyData,
        completeness);

    return new AnalysisResponse(
        GeneratedAtUtc: generatedAtUtc,
        LocalDate: localDate,
        TimeZoneId: timeZoneId,
        Components: [activity, recovery, nutrition, consistency],
        BodyData: bodyData,
        Completeness: completeness,
        Observations: observations);
}

static AnalysisComponent BuildActivityComponent(IReadOnlyCollection<PulseBoard.Api.Models.DailyActivity> dailyActivities)
{
    if (dailyActivities.Count == 0)
    {
        return new AnalysisComponent(
            "activity",
            "Actividad",
            "Activity",
            null,
            "insufficient",
            "Faltan datos de actividad. Apple Health puede enviarlos con un Atajo.",
            "Activity data is missing. Apple Health can send it with a Shortcut.",
            []);
    }

    var loggedDays = dailyActivities.Select(activity => activity.LocalDate).Distinct().Count();
    var averageSteps = dailyActivities.Average(activity => activity.Steps);
    var averageExerciseMinutes = dailyActivities.Average(activity => activity.ExerciseMinutes);
    var averageActiveEnergy = dailyActivities.Average(activity => activity.ActiveEnergyKcal);
    var loggingScore = Math.Clamp(loggedDays / 7m * 100m, 0m, 100m);
    var stepsSignal = Math.Clamp((decimal)averageSteps / 8_000m * 100m, 0m, 100m);
    var exerciseSignal = Math.Clamp((decimal)averageExerciseMinutes / 30m * 100m, 0m, 100m);
    var score = (int)Math.Round(loggingScore * 0.35m + stepsSignal * 0.35m + exerciseSignal * 0.3m, 0);

    return new AnalysisComponent(
        "activity",
        "Actividad",
        "Activity",
        score,
        ScoreStatus(score),
        $"Actividad en {loggedDays}/7 dias. Media: {Math.Round(averageSteps, 0)} pasos y {Math.Round(averageExerciseMinutes, 0)} min de ejercicio.",
        $"Activity on {loggedDays}/7 days. Average: {Math.Round(averageSteps, 0)} steps and {Math.Round(averageExerciseMinutes, 0)} exercise minutes.",
        [
            $"loggedDays7:{loggedDays}",
            $"stepsAverage:{Math.Round(averageSteps, 0)}",
            $"exerciseMinutesAverage:{Math.Round(averageExerciseMinutes, 0)}",
            $"activeEnergyAverageKcal:{Math.Round(averageActiveEnergy, 0)}"
        ]);
}

static AnalysisComponent BuildRecoveryComponent(IReadOnlyList<PulseBoard.Api.Models.CheckIn> checkIns)
{
    var recent = checkIns.Take(7).ToArray();
    if (recent.Length == 0)
    {
        return new AnalysisComponent(
            "recovery",
            "Recuperacion",
            "Recovery",
            null,
            "insufficient",
            "Faltan check-ins para evaluar recuperacion.",
            "Check-ins are needed to evaluate recovery.",
            []);
    }

    var averageSleep = recent.Average(checkIn => checkIn.SleepHours);
    var averageEnergy = recent.Average(checkIn => (decimal)checkIn.Energy);
    var sleepScore = Math.Clamp(averageSleep / 8m * 100m, 0m, 100m);
    var subjectiveScore = recent.Average(checkIn =>
        (checkIn.SleepQuality + checkIn.Energy + checkIn.Mood + checkIn.Recovery + (6 - checkIn.Fatigue) + (6 - checkIn.Stress)) / 6m * 20m);
    var score = (int)Math.Round(sleepScore * 0.35m + subjectiveScore * 0.65m, 0);

    return new AnalysisComponent(
        "recovery",
        "Recuperacion",
        "Recovery",
        score,
        ScoreStatus(score),
        $"Promedio reciente: {Round(averageSleep)} h de sueno, energia {Round(averageEnergy)}/5.",
        $"Recent average: {Round(averageSleep)} h of sleep, energy {Round(averageEnergy)}/5.",
        [
            $"checkIns:{recent.Length}",
            $"sleepAverageHours:{Round(averageSleep)}",
            $"energyAverage:{Round(averageEnergy)}"
        ]);
}

static AnalysisComponent BuildNutritionComponent(IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals)
{
    if (meals.Count == 0)
    {
        return new AnalysisComponent(
            "nutrition",
            "Alimentacion",
            "Nutrition",
            null,
            "insufficient",
            "Faltan comidas registradas para analizar alimentacion.",
            "Logged meals are needed to analyze nutrition.",
            []);
    }

    var loggedDays = meals.Select(meal => meal.LocalDate).Distinct().Count();
    var proteinAverage = meals.Sum(meal => meal.ProteinGrams) / loggedDays;
    var vegetableDays = meals
        .Where(meal => meal.HasVegetables)
        .Select(meal => meal.LocalDate)
        .Distinct()
        .Count();
    var loggingScore = Math.Clamp(loggedDays / 7m * 100m, 0m, 100m);
    var proteinSignal = Math.Clamp(proteinAverage / 100m * 100m, 0m, 100m);
    var vegetableSignal = Math.Clamp(vegetableDays / 7m * 100m, 0m, 100m);
    var score = (int)Math.Round(loggingScore * 0.45m + proteinSignal * 0.35m + vegetableSignal * 0.2m, 0);

    return new AnalysisComponent(
        "nutrition",
        "Alimentacion",
        "Nutrition",
        score,
        ScoreStatus(score),
        $"Hay comidas en {loggedDays}/7 dias. Proteina media registrada: {Round(proteinAverage)} g/dia.",
        $"Meals are logged on {loggedDays}/7 days. Logged protein average: {Round(proteinAverage)} g/day.",
        [
            $"loggedDays7:{loggedDays}",
            $"proteinAverageGrams:{Round(proteinAverage)}",
            $"vegetableDays7:{vegetableDays}"
        ]);
}

static AnalysisComponent BuildConsistencyComponent(
    string[] recentDates7,
    IReadOnlyCollection<PulseBoard.Api.Models.Habit> habits,
    IReadOnlyCollection<PulseBoard.Api.Models.HabitCompletion> completions,
    IReadOnlyList<PulseBoard.Api.Models.CheckIn> checkIns)
{
    var checkInDays = checkIns
        .Where(checkIn => recentDates7.Contains(checkIn.LocalDate))
        .Select(checkIn => checkIn.LocalDate)
        .Distinct()
        .Count();
    var expectedHabitCompletions = habits.Count * recentDates7.Length;
    var habitRate = expectedHabitCompletions == 0
        ? (decimal?)null
        : Math.Clamp((decimal)completions.Count / expectedHabitCompletions * 100m, 0m, 100m);

    if (habitRate is null && checkInDays == 0)
    {
        return new AnalysisComponent(
            "consistency",
            "Constancia",
            "Consistency",
            null,
            "insufficient",
            "Faltan habitos o check-ins recientes para medir constancia.",
            "Recent habits or check-ins are needed to measure consistency.",
            []);
    }

    var checkInRate = checkInDays / 7m * 100m;
    var score = habitRate is null
        ? (int)Math.Round(checkInRate, 0)
        : (int)Math.Round(habitRate.Value * 0.65m + checkInRate * 0.35m, 0);

    return new AnalysisComponent(
        "consistency",
        "Constancia",
        "Consistency",
        score,
        ScoreStatus(score),
        $"Check-ins en {checkInDays}/7 dias. Cumplimiento de habitos: {Round(habitRate ?? 0)}%.",
        $"Check-ins on {checkInDays}/7 days. Habit completion: {Round(habitRate ?? 0)}%.",
        [
            $"checkInDays7:{checkInDays}",
            $"habitCompletionRate7:{Round(habitRate ?? 0)}"
        ]);
}

static BodyDataSignal BuildBodyDataSignal(
    IReadOnlyCollection<PulseBoard.Api.Models.BodyMeasurement> measurements,
    IReadOnlyList<TrendMetric> bodyTrends)
{
    var weightTrend = bodyTrends.FirstOrDefault(trend => trend.Key == "weight");
    var trendCode = weightTrend?.Trend ?? "insufficient";
    var summaryEs = trendCode == "insufficient"
        ? "Aun faltan mediciones para una tendencia corporal confiable."
        : $"Tendencia de peso a 30 dias: {weightTrend!.TrendEs.ToLowerInvariant()}.";
    var summaryEn = trendCode == "insufficient"
        ? "More measurements are needed for a reliable body trend."
        : $"30 day weight trend: {weightTrend!.TrendEn.ToLowerInvariant()}.";

    return new BodyDataSignal(
        Trend: trendCode,
        SummaryEs: summaryEs,
        SummaryEn: summaryEn,
        DataPoints: measurements.Count,
        Trends: bodyTrends.ToArray());
}

static DataCompleteness BuildDataCompleteness(
    IReadOnlyCollection<PulseBoard.Api.Models.CheckIn> checkIns,
    IReadOnlyCollection<PulseBoard.Api.Models.Habit> habits,
    IReadOnlyCollection<PulseBoard.Api.Models.HabitCompletion> completions,
    IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals,
    IReadOnlyCollection<PulseBoard.Api.Models.DailyActivity> dailyActivities,
    IReadOnlyCollection<PulseBoard.Api.Models.BodyMeasurement> measurements)
{
    var present = new List<string>();
    var missing = new List<string>();

    AddCompletenessSignal(checkIns.Count > 0, "check-in", present, missing);
    AddCompletenessSignal(habits.Count > 0 && completions.Count > 0, "habits", present, missing);
    AddCompletenessSignal(meals.Count > 0, "nutrition", present, missing);
    AddCompletenessSignal(measurements.Count >= 3, "body", present, missing);
    AddCompletenessSignal(dailyActivities.Count > 0, "activity", present, missing);

    var score = (int)Math.Round(present.Count / 5m * 100m, 0);

    return new DataCompleteness(
        Score: score,
        PresentDomains: present.ToArray(),
        MissingDomains: missing.ToArray(),
        SummaryEs: $"Datos disponibles en {present.Count}/5 areas.",
        SummaryEn: $"Data is available in {present.Count}/5 areas.");
}

static void AddCompletenessSignal(bool hasData, string key, ICollection<string> present, ICollection<string> missing)
{
    if (hasData)
    {
        present.Add(key);
        return;
    }

    missing.Add(key);
}

static AnalysisObservation[] BuildAnalysisObservations(
    IReadOnlyList<PulseBoard.Api.Models.CheckIn> checkIns,
    IReadOnlyCollection<PulseBoard.Api.Models.Habit> habits,
    IReadOnlyCollection<PulseBoard.Api.Models.HabitCompletion> completions,
    IReadOnlyCollection<PulseBoard.Api.Models.Meal> meals,
    IReadOnlyCollection<PulseBoard.Api.Models.DailyActivity> dailyActivities,
    BodyDataSignal bodyData,
    DataCompleteness completeness)
{
    var observations = new List<AnalysisObservation>();
    var recentCheckIns = checkIns.Take(7).ToArray();

    if (recentCheckIns.Length >= 3)
    {
        var averageSleep = recentCheckIns.Average(checkIn => checkIn.SleepHours);
        var averageEnergy = recentCheckIns.Average(checkIn => (decimal)checkIn.Energy);
        if (averageSleep < 7 && averageEnergy <= 3)
        {
            observations.Add(new AnalysisObservation(
                "recovery",
                "warning",
                "El sueno medio reciente esta por debajo de 7 horas y la energia registrada no esta alta.",
                "Recent average sleep is below 7 hours and logged energy is not high.",
                "sleepAverageHours<7 && energyAverage<=3"));
        }
    }

    if (habits.Count > 0)
    {
        var completionRate = Math.Round((decimal)completions.Count / (habits.Count * 7) * 100m, 0);
        if (completionRate >= 80)
        {
            observations.Add(new AnalysisObservation(
                "habits",
                "positive",
                "La constancia semanal de habitos esta alta.",
                "Weekly habit consistency is high.",
                "habitCompletionRate7>=80"));
        }
        else if (completionRate > 0)
        {
            observations.Add(new AnalysisObservation(
                "habits",
                "info",
                "Hay margen para subir la constancia semanal de habitos.",
                "There is room to improve weekly habit consistency.",
                "0<habitCompletionRate7<80"));
        }
    }

    if (dailyActivities.Count == 0)
    {
        observations.Add(new AnalysisObservation(
            "activity",
            "info",
            "Aun faltan datos de Apple Health para analizar actividad.",
            "Apple Health data is still needed to analyze activity.",
            "activityDays7==0"));
    }
    else
    {
        var loggedDays = dailyActivities.Select(activity => activity.LocalDate).Distinct().Count();
        var averageExerciseMinutes = dailyActivities.Average(activity => activity.ExerciseMinutes);
        if (averageExerciseMinutes >= 30)
        {
            observations.Add(new AnalysisObservation(
                "activity",
                "positive",
                "Los minutos de ejercicio registrados por Apple Health estan en buen nivel.",
                "Exercise minutes logged by Apple Health are at a good level.",
                "exerciseMinutesAverage>=30"));
        }
        else
        {
            observations.Add(new AnalysisObservation(
                "activity",
                "info",
                $"Hay actividad registrada en {loggedDays}/7 dias.",
                $"Activity is logged on {loggedDays}/7 days.",
                "activityDays7>0"));
        }
    }

    if (meals.Count == 0)
    {
        observations.Add(new AnalysisObservation(
            "nutrition",
            "info",
            "Aun faltan comidas para relacionar alimentacion con energia y recuperacion.",
            "More meals are needed before nutrition can be related to energy and recovery.",
            "meals7==0"));
    }
    else
    {
        var loggedDays = meals.Select(meal => meal.LocalDate).Distinct().Count();
        var proteinAverage = meals.Sum(meal => meal.ProteinGrams) / loggedDays;
        if (proteinAverage >= 100)
        {
            observations.Add(new AnalysisObservation(
                "nutrition",
                "positive",
                "La proteina media registrada ya es alta.",
                "Logged average protein is already high.",
                "proteinAverageGrams>=100"));
        }

        if (meals.All(meal => !meal.HasVegetables))
        {
            observations.Add(new AnalysisObservation(
                "nutrition",
                "info",
                "No hay comidas recientes marcadas con verduras.",
                "No recent meals are marked with vegetables.",
                "vegetableMeals7==0"));
        }
    }

    observations.Add(new AnalysisObservation(
        "body",
        "info",
        bodyData.SummaryEs,
        bodyData.SummaryEn,
        "bodyTrend30d"));

    if (completeness.MissingDomains.Length > 0)
    {
        observations.Add(new AnalysisObservation(
            "data",
            "info",
            $"Faltan datos en: {string.Join(", ", completeness.MissingDomains)}.",
            $"Missing data in: {string.Join(", ", completeness.MissingDomains)}.",
            "dataCompleteness<100"));
    }

    if (observations.Count == 0)
    {
        observations.Add(new AnalysisObservation(
            "data",
            "info",
            "Aun se estan acumulando datos para generar observaciones mas utiles.",
            "Data is still being accumulated for more useful observations.",
            "fallback"));
    }

    return observations.Take(6).ToArray();
}

static string ScoreStatus(int score)
{
    if (score >= 80)
    {
        return "strong";
    }

    if (score >= 55)
    {
        return "steady";
    }

    return "needs-data";
}

static async Task<PulseBoard.Api.Models.DailyActivity> UpsertDailyActivityAsync(
    PulseBoardDbContext db,
    PulseBoard.Api.Models.DailyActivity activity)
{
    var existing = await db.DailyActivities.FirstOrDefaultAsync(current =>
        current.UserId == activity.UserId
        && current.LocalDate == activity.LocalDate
        && current.Source == activity.Source);

    if (existing is null)
    {
        db.DailyActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity;
    }

    db.DailyActivities.Remove(existing);
    await db.SaveChangesAsync();

    db.DailyActivities.Add(activity);
    await db.SaveChangesAsync();

    return activity;
}

static bool TryBuildDailyActivityRequest(
    JsonElement payload,
    out CreateDailyActivityRequest? request,
    out Dictionary<string, string[]> errors)
{
    request = null;
    errors = new Dictionary<string, string[]>();

    var localDate = ReadString(payload, "localDate", errors);
    var steps = ReadInt(payload, "steps", errors);
    var activeEnergyKcal = ReadInt(payload, "activeEnergyKcal", errors);
    var exerciseMinutes = ReadInt(payload, "exerciseMinutes", errors);
    var walkingRunningDistanceKm = ReadNullableDecimal(payload, "walkingRunningDistanceKm", errors) ?? 0;
    var cyclingDistanceKm = ReadNullableDecimal(payload, "cyclingDistanceKm", errors) ?? 0;
    var workoutCount = ReadInt(payload, "workoutCount", errors);
    var notes = ReadOptionalString(payload, "notes");

    if (errors.Count > 0)
    {
        return false;
    }

    request = new CreateDailyActivityRequest(
        localDate!,
        steps!.Value,
        activeEnergyKcal!.Value,
        exerciseMinutes!.Value,
        walkingRunningDistanceKm,
        cyclingDistanceKm,
        workoutCount!.Value,
        notes);

    foreach (var error in request.Validate())
    {
        errors[error.Key] = error.Value;
    }

    return errors.Count == 0;
}

static string? ReadString(JsonElement payload, string propertyName, IDictionary<string, string[]> errors)
{
    if (!payload.TryGetProperty(propertyName, out var value))
    {
        errors[propertyName] = [$"{propertyName} is required."];
        return null;
    }

    if (value.ValueKind == JsonValueKind.String)
    {
        var text = value.GetString()?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
    }

    errors[propertyName] = [$"{propertyName} must be text."];
    return null;
}

static string? ReadOptionalString(JsonElement payload, string propertyName)
{
    if (!payload.TryGetProperty(propertyName, out var value))
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : value.ToString();
}

static int? ReadInt(JsonElement payload, string propertyName, IDictionary<string, string[]> errors)
{
    var value = ReadDecimal(payload, propertyName, errors);
    return value is null ? null : (int)Math.Round(value.Value, 0, MidpointRounding.AwayFromZero);
}

static decimal? ReadNullableDecimal(JsonElement payload, string propertyName, IDictionary<string, string[]> errors)
{
    if (!payload.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
    {
        return null;
    }

    return ConvertJsonElementToDecimal(value, propertyName, errors);
}

static decimal? ReadDecimal(JsonElement payload, string propertyName, IDictionary<string, string[]> errors)
{
    if (!payload.TryGetProperty(propertyName, out var value))
    {
        errors[propertyName] = [$"{propertyName} is required."];
        return null;
    }

    return ConvertJsonElementToDecimal(value, propertyName, errors);
}

static decimal? ConvertJsonElementToDecimal(JsonElement value, string propertyName, IDictionary<string, string[]> errors)
{
    if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
    {
        return number;
    }

    if (value.ValueKind == JsonValueKind.String)
    {
        var text = value.GetString()?.Trim() ?? string.Empty;
        var match = Regex.Match(text, @"-?\d+(?:[\.,]\d+)?");
        if (match.Success
            && decimal.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }
    }

    errors[propertyName] = [$"{propertyName} must be a number. Received: {value}"];
    return null;
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

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "Meals" (
            "Id" uuid NOT NULL,
            "UserId" character varying(80) NOT NULL,
            "LocalDate" character varying(10) NOT NULL,
            "TimeZoneId" character varying(80) NOT NULL,
            "Name" character varying(160) NOT NULL,
            "MealType" character varying(40) NOT NULL,
            "CaloriesKcal" integer NOT NULL,
            "ProteinGrams" numeric(7,2) NOT NULL,
            "CarbohydrateGrams" numeric(7,2) NOT NULL,
            "FatGrams" numeric(7,2) NOT NULL,
            "HasVegetables" boolean NOT NULL,
            "IsFavorite" boolean NOT NULL,
            "Notes" character varying(1000) NULL,
            "EatenAtUtc" timestamp with time zone NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_Meals" PRIMARY KEY ("Id")
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_Meals_UserId_LocalDate"
            ON "Meals" ("UserId", "LocalDate");
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_Meals_UserId_IsFavorite"
            ON "Meals" ("UserId", "IsFavorite");
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "DailyActivities" (
            "Id" uuid NOT NULL,
            "UserId" character varying(80) NOT NULL,
            "LocalDate" character varying(10) NOT NULL,
            "TimeZoneId" character varying(80) NOT NULL,
            "Steps" integer NOT NULL,
            "ActiveEnergyKcal" integer NOT NULL,
            "ExerciseMinutes" integer NOT NULL,
            "WalkingRunningDistanceKm" numeric(8,2) NULL,
            "CyclingDistanceKm" numeric(8,2) NULL,
            "WorkoutCount" integer NOT NULL,
            "Source" character varying(40) NOT NULL,
            "Notes" character varying(1000) NULL,
            "RecordedAtUtc" timestamp with time zone NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_DailyActivities" PRIMARY KEY ("Id")
        );
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_DailyActivities_UserId_LocalDate_Source"
            ON "DailyActivities" ("UserId", "LocalDate", "Source");
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
    NutritionSummary Nutrition,
    ActivitySummary Activity,
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

public sealed record NutritionSummary(
    NutritionTotals Today,
    NutritionTotals Average7Days,
    int LoggedDays7,
    PulseBoard.Api.Models.Meal[] LatestMeals);

public sealed record NutritionTotals(
    decimal Meals,
    decimal CaloriesKcal,
    decimal ProteinGrams,
    decimal CarbohydrateGrams,
    decimal FatGrams,
    decimal VegetableMeals);

public sealed record ActivitySummary(
    ActivityTotals Today,
    ActivityTotals Average7Days,
    int LoggedDays7,
    PulseBoard.Api.Models.DailyActivity[] LatestActivities);

public sealed record ActivityTotals(
    decimal Steps,
    decimal ActiveEnergyKcal,
    decimal ExerciseMinutes,
    decimal WalkingRunningDistanceKm,
    decimal CyclingDistanceKm,
    decimal WorkoutCount);

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

public sealed record AnalysisResponse(
    DateTimeOffset GeneratedAtUtc,
    string LocalDate,
    string TimeZoneId,
    AnalysisComponent[] Components,
    BodyDataSignal BodyData,
    DataCompleteness Completeness,
    AnalysisObservation[] Observations);

public sealed record AnalysisComponent(
    string Key,
    string LabelEs,
    string LabelEn,
    int? Score,
    string Status,
    string SummaryEs,
    string SummaryEn,
    string[] Evidence);

public sealed record BodyDataSignal(
    string Trend,
    string SummaryEs,
    string SummaryEn,
    int DataPoints,
    TrendMetric[] Trends);

public sealed record DataCompleteness(
    int Score,
    string[] PresentDomains,
    string[] MissingDomains,
    string SummaryEs,
    string SummaryEn);

public sealed record AnalysisObservation(
    string Category,
    string Severity,
    string MessageEs,
    string MessageEn,
    string Rule);

public sealed record MeasurementPoint(DateTimeOffset MeasuredAtUtc, decimal? Value);
