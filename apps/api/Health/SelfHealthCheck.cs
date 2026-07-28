using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PulseBoard.Api.Health;

public sealed class SelfHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("PulseBoard API is running."));
}
