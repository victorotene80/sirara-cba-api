using System.ComponentModel.DataAnnotations;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Observability;

/// <summary>
/// Where this deployment's traces, metrics and logs go.
/// </summary>
/// <remarks>
/// Every exporter is a configuration choice, never a literal (ER-9): the same build
/// runs against a collector in production, a console in a developer's terminal, and
/// neither in a test. The instrumentation itself is not optional — it is wired in
/// S01 because retrofitting it across twelve slices is the cost `architecture.md`
/// §8 exists to avoid (D-16).
/// </remarks>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>Identifies this service in every span, metric and log it emits.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// OTLP collector endpoint. Absent means no collector is exported to — which is
    /// the correct state for a test host, not a misconfiguration.
    /// </summary>
    public Uri? OtlpEndpoint { get; init; }

    /// <summary>
    /// Writes spans and metrics to the console. Opt-in for local debugging; off
    /// everywhere else, including Development, so that a test run stays readable.
    /// </summary>
    public bool ConsoleExporter { get; init; }
}
