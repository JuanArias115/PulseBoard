using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PulseBoard.Api.Tests;

public sealed class ApiSmokeTests
{
    [Fact]
    public async Task Meta_Returns_Project_Defaults()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/meta");
        var body = await response.Content.ReadFromJsonAsync<ApiMetaResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PulseBoard", body?.Name);
        Assert.Equal("Europe/Vienna", body?.TimeZoneId);
        Assert.Contains("es", body?.SupportedLanguages ?? []);
        Assert.Contains("en", body?.SupportedLanguages ?? []);
    }

    [Fact]
    public async Task BodyMeasurement_Can_Be_Created()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/body-measurements", new
        {
            measuredAt = DateTimeOffset.Parse("2026-07-28T07:00:00+02:00"),
            weightKg = 74.2m,
            bodyFatPercentage = 19.8m,
            musclePercentage = 42.1m,
            bodyWaterPercentage = 55.1m,
            bodyMassIndex = 23.4m,
            estimatedCaloriesKcal = 3087,
            notes = "Soehnle manual entry"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Returns_Trends_And_Vienna_Timezone()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        await client.PostAsJsonAsync("/api/v1/body-measurements", new
        {
            measuredAt = DateTimeOffset.UtcNow.AddDays(-2),
            weightKg = 74.8m,
            bodyFatPercentage = 20.1m,
            musclePercentage = 42.0m,
            bodyWaterPercentage = 55.0m,
            bodyMassIndex = 23.6m,
            estimatedCaloriesKcal = 3087,
            notes = "Trend point"
        });
        await client.PostAsJsonAsync("/api/v1/body-measurements", new
        {
            measuredAt = DateTimeOffset.UtcNow.AddDays(-1),
            weightKg = 74.4m,
            bodyFatPercentage = 19.9m,
            musclePercentage = 42.1m,
            bodyWaterPercentage = 55.1m,
            bodyMassIndex = 23.5m,
            estimatedCaloriesKcal = 3087,
            notes = "Trend point"
        });
        await client.PostAsJsonAsync("/api/v1/body-measurements", new
        {
            measuredAt = DateTimeOffset.UtcNow,
            weightKg = 74.2m,
            bodyFatPercentage = 19.8m,
            musclePercentage = 42.1m,
            bodyWaterPercentage = 55.1m,
            bodyMassIndex = 23.4m,
            estimatedCaloriesKcal = 3087,
            notes = "Trend point"
        });

        var response = await client.GetAsync("/api/v1/dashboard");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Europe/Vienna", body.GetProperty("timeZoneId").GetString());
        Assert.True(body.GetProperty("body").GetProperty("trends").GetArrayLength() >= 4);
        Assert.True(body.GetProperty("body").GetProperty("history").GetArrayLength() >= 3);
    }

    [Fact]
    public async Task Dashboard_Works_With_No_Data()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, body.GetProperty("readinessScore").GetInt32());
        Assert.True(body.GetProperty("insights").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Meal_Can_Be_Created_And_Summarized()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var mealResponse = await client.PostAsJsonAsync("/api/v1/meals", new
        {
            localDate = "2026-07-28",
            eatenAt = DateTimeOffset.Parse("2026-07-28T12:30:00+02:00"),
            name = "Pollo con arroz",
            mealType = "lunch",
            caloriesKcal = 720,
            proteinGrams = 48,
            carbohydrateGrams = 82,
            fatGrams = 18,
            hasVegetables = true,
            isFavorite = true,
            notes = ""
        });

        var summaryResponse = await client.GetAsync("/api/v1/nutrition-summary?localDate=2026-07-28");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, mealResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.Equal(720, summary.GetProperty("today").GetProperty("caloriesKcal").GetDecimal());
        Assert.Equal(48, summary.GetProperty("today").GetProperty("proteinGrams").GetDecimal());
    }

    [Fact]
    public async Task AppleHealthBridge_Requires_Key()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/integrations/apple-health/body-measurements", new
        {
            measuredAt = DateTimeOffset.Parse("2026-07-28T07:00:00+02:00"),
            weightKg = 74.2m,
            bodyFatPercentage = 19.8m,
            musclePercentage = 42.1m,
            bodyWaterPercentage = 55.1m,
            bodyMassIndex = 23.4m,
            estimatedCaloriesKcal = 3087,
            notes = "Apple Health"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AppleHealthBridge_Can_Create_Measurement_With_Key()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/apple-health/body-measurements")
        {
            Content = JsonContent.Create(new
            {
                measuredAt = DateTimeOffset.Parse("2026-07-29T07:00:00+02:00"),
                weightKg = 74.1m,
                bodyFatPercentage = 19.7m,
                musclePercentage = 42.2m,
                bodyWaterPercentage = 55.2m,
                bodyMassIndex = 23.3m,
                estimatedCaloriesKcal = 3087,
                notes = "Apple Health"
            })
        };
        request.Headers.Add("X-PulseBoard-Bridge-Key", "test-bridge-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task HabitCompletion_Can_Be_Created()
    {
        using var application = CreateApplication();
        var client = application.CreateClient();

        var habitResponse = await client.PostAsJsonAsync("/api/v1/habits", new
        {
            name = "Creatina",
            category = "supplement",
            frequency = "daily",
            targetAmount = 5,
            unit = "g",
            notes = ""
        });

        var habit = await habitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var habitId = habit.GetProperty("id").GetGuid();

        var completionResponse = await client.PostAsJsonAsync($"/api/v1/habits/{habitId}/completions", new
        {
            localDate = "2026-07-28",
            amount = 5,
            notes = ""
        });

        Assert.Equal(HttpStatusCode.Created, completionResponse.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateApplication()
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PulseBoard:UseInMemory", "true");
                builder.UseSetting("PULSEBOARD_APPLE_HEALTH_BRIDGE_KEY", "test-bridge-key");
            });

    private sealed record ApiMetaResponse(
        string Name,
        string Status,
        string DefaultLanguage,
        string[] SupportedLanguages,
        string TimeZoneId,
        string Units,
        string[] Modules);

}
