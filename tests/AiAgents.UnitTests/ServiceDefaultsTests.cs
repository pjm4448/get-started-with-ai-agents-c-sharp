using System.Net;
using AiAgents.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace AiAgents.UnitTests;

public sealed class ServiceDefaultsTests
{
    [Fact]
    public async Task ReadinessFailuresDoNotFailLiveness()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddHealthChecks()
            .AddCheck("dependency", () => HealthCheckResult.Unhealthy("Not ready."));
        using var host = builder.Build();
        var health = host.Services.GetRequiredService<HealthCheckService>();

        var readiness = await health.CheckHealthAsync();
        var liveness = await health.CheckHealthAsync(check => check.Tags.Contains("live"));

        Assert.Equal(HealthStatus.Unhealthy, readiness.Status);
        Assert.Equal(HealthStatus.Healthy, liveness.Status);
        Assert.Single(liveness.Entries);
    }

    [Fact]
    public async Task DoesNotRetryGenerativePostRequests()
    {
        var handler = new ThrottledHandler();
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddHttpClient("agent")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var host = builder.Build();
        using var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("agent");

        using var response = await client.PostAsync("https://example.test/responses", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class ThrottledHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        }
    }
}
