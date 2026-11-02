using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace SiraraCBA.Api.SmokeTests;

/// <summary>
/// S01's definition of done says the host "starts and serves a health check",
/// demonstrated by a test that boots the host, and that a request emits a trace
/// carrying its correlation id. Both were asserted for twelve steps and never shown.
/// </summary>
/// <remarks>
/// The host is booted in-process and touches no database — that is what keeps this
/// suite under E2 rather than E3, which means Testcontainers PostgreSQL (D-13).
/// </remarks>
public sealed class HostStartupTests : IClassFixture<ApiHostFixture>
{
    private readonly ApiHostFixture _host;

    public HostStartupTests(ApiHostFixture host) => _host = host;

    [Fact]
    public async Task TheHost_Starts_AndServesItsHealthCheck()
    {
        using HttpClient client = _host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public void TheHost_Boots_WithEveryOptionsSectionValidated()
    {
        // ValidateOnStart means a misconfigured deployment fails at boot rather than
        // at 2 a.m. under load. Resolving the client is what forces the host to build,
        // so a bad Currencies, BusinessCalendar or Telemetry section fails here.
        using HttpClient client = _host.CreateClient();

        _host.Services.GetService<TracerProvider>()
            .ShouldNotBeNull("the host must register telemetry (architecture.md §8, D-16).");
    }

    [Fact]
    public async Task ARequest_EmitsATrace_CarryingItsCorrelationId()
    {
        // Exported, not merely created: ASP.NET Core produces an Activity for any
        // listener that asks, so capturing one with a bare ActivityListener would pass
        // with no telemetry configured at all. Reaching OpenTelemetry's own export
        // pipeline is what makes this discriminating.
        int before = _host.Spans.Count;

        using HttpClient client = _host.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.IsSuccessStatusCode.ShouldBeTrue();

        IReadOnlyList<Activity> spans = _host.Spans;
        spans.Count.ShouldBeGreaterThan(before, "the request emitted no span.");

        Activity request = spans[^1];

        // The correlation id is the trace id: every log, metric and downstream call
        // for this request carries it, which is the point of §8.
        request.TraceId.ShouldNotBe(default);
        request.RootId.ShouldNotBeNullOrWhiteSpace();
    }
}
