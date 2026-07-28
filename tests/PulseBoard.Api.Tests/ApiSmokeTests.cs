using System.Net;
using System.Net.Http.Json;
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

    private static WebApplicationFactory<Program> CreateApplication()
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PulseBoard:UseInMemory", "true");
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
