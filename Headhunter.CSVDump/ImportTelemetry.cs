using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Headhunter.CSVDump;

/// <summary>
/// One trace span, one duration and one row count per import step, plus a line on the console, so a slow import
/// shows which step is slow. Spans and metrics go out over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set (see Program).
/// </summary>
public static class ImportTelemetry
{
    public const string Name = "Headhunter.Import";

    internal static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("headhunter.import.step.duration", "s", "Time spent in an import step");
    private static readonly Counter<long> RowCount =
        Meter.CreateCounter<long>("headhunter.import.rows", "{row}", "Rows an import step read or wrote");

    /// <summary>Runs one step: times it, counts the rows it reports, and marks it failed if it throws.</summary>
    public static async Task RunAsync(string name, Func<ImportStep, Task> body)
    {
        using var activity = Source.StartActivity(name);
        var step = new ImportStep();
        var clock = Stopwatch.StartNew();
        Console.WriteLine($"[import] {name} started");
        var failed = true;
        try
        {
            await body(step);
            failed = false;
        }
        finally
        {
            clock.Stop();
            var tags = new TagList { { "step", name }, { "outcome", failed ? "failed" : "ok" } };
            Duration.Record(clock.Elapsed.TotalSeconds, tags);
            if (step.Rows > 0) RowCount.Add(step.Rows, tags);
            activity?.SetTag("import.rows", step.Rows);
            if (failed) activity?.SetStatus(ActivityStatusCode.Error);

            var rate = step.Rows > 0 && clock.Elapsed.TotalSeconds > 0 ? $", {step.Rows / clock.Elapsed.TotalSeconds:N0} rows/s" : "";
            Console.WriteLine($"[import] {name} {(failed ? "FAILED after" : "took")} {clock.Elapsed:hh\\:mm\\:ss\\.f}" + (step.Rows > 0 ? $", {step.Rows:N0} rows{rate}" : ""));
        }
    }
}

/// <summary>What a running import step reports back: set <see cref="Rows"/> to the rows it read or wrote.</summary>
public sealed class ImportStep
{
    public long Rows { get; set; }
}
