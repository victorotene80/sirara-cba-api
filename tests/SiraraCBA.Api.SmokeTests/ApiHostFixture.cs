using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace SiraraCBA.Api.SmokeTests;

/// <summary>
/// The real host, booted once, with an in-memory span exporter attached to the
/// telemetry it configures for itself.
/// </summary>
/// <remarks>
/// The exporter is attached here rather than through `WithWebHostBuilder` in a test.
/// Deriving a second factory leaves two `TracerProvider`s listening to the same
/// ASP.NET Core instrumentation, and the request's span is exported by whichever
/// was built first — so that test passed alone and failed beside its siblings.
/// A suite isolated by test order is not isolated (coding-standards.md, Testing).
/// One host, one provider, one exporter.
/// </remarks>
public sealed class ApiHostFixture : WebApplicationFactory<Program>
{
    private readonly List<Activity> _spans = [];

    /// <summary>Spans this host has exported, oldest first.</summary>
    public IReadOnlyList<Activity> Spans
    {
        get
        {
            Services.GetRequiredService<TracerProvider>().ForceFlush();

            lock (_spans)
            {
                return [.. _spans];
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureServices(services =>
            services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
                tracing.AddInMemoryExporter(_spans)));
    }
}
