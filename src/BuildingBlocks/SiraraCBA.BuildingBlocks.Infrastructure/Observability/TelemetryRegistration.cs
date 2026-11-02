using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Wires OpenTelemetry traces, metrics and logs (architecture.md §8, D-16).
/// </summary>
/// <remarks>
/// Lives here rather than in the host so that the composition root stays a
/// composition root (§7), and so that a module extracted to its own host later
/// inherits the same telemetry by calling the same method.
/// </remarks>
public static class TelemetryRegistration
{
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TelemetryOptions>()
            .Bind(configuration.GetSection(TelemetryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Read at composition time because the exporter pipeline is built once, at
        // startup, before any IOptions consumer exists. ValidateOnStart above still
        // fails the boot on a bad section.
        TelemetryOptions options =
            configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>()
            ?? new TelemetryOptions();

        // No HttpClient instrumentation: nothing in S01 makes an outbound call, and
        // OpenTelemetry.Instrumentation.Http is a package the slice's plan does not
        // list. It arrives with the first module that calls out.
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName))
            .WithTracing(tracing =>
            {
                // Every request gets a span, and its trace id is the correlation id
                // that every log and downstream call for that request carries.
                tracing.AddAspNetCoreInstrumentation();
                ApplyExporters(options, tracing.AddConsoleExporter, tracing.AddOtlpExporter);
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddRuntimeInstrumentation();
                ApplyExporters(options, metrics.AddConsoleExporter, metrics.AddOtlpExporter);
            });

        services.AddLogging(logging => logging.AddOpenTelemetry(open =>
        {
            open.IncludeScopes = true;
            open.IncludeFormattedMessage = true;
            open.ParseStateValues = true;
        }));

        return services;
    }

    private static void ApplyExporters<TBuilder>(
        TelemetryOptions options,
        Func<TBuilder> addConsole,
        Func<Action<OpenTelemetry.Exporter.OtlpExporterOptions>, TBuilder> addOtlp)
    {
        if (options.ConsoleExporter)
        {
            addConsole();
        }

        if (options.OtlpEndpoint is not null)
        {
            addOtlp(exporter => exporter.Endpoint = options.OtlpEndpoint);
        }
    }
}
