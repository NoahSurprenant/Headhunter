using Headhunter.API.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Net;

namespace Headhunter.Tests.Telemetry;

[TestFixture]
[NonParallelizable] // The pipeline test points the static Log.Logger, which Serilog request logging writes to, at its sink.
public class RequestTelemetryTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];
        public void Emit(LogEvent logEvent)
        {
            lock (Events)
                Events.Add(logEvent);
        }
    }

    [Test]
    public void FormatTraceResponse_NoActivity_IsNull()
    {
        Assert.That(RequestTelemetryMiddleware.FormatTraceResponse(null), Is.Null);
    }

    [Test]
    public void FormatTraceResponse_HierarchicalActivity_IsNull()
    {
        using var activity = new Activity("legacy");
        activity.SetIdFormat(ActivityIdFormat.Hierarchical);
        activity.Start();
        Assert.That(RequestTelemetryMiddleware.FormatTraceResponse(activity), Is.Null);
    }

    [TestCase(false, "00")]
    [TestCase(true, "01")]
    public void FormatTraceResponse_W3C(bool recorded, string flags)
    {
        using var activity = new Activity("request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.ActivityTraceFlags = recorded ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
        activity.Start();

        Assert.That(RequestTelemetryMiddleware.FormatTraceResponse(activity),
            Is.EqualTo($"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{flags}"));
    }

    [Test]
    public async Task Middleware_SetsTraceResponse_AndPushesClientIp()
    {
        using var activity = new Activity("request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        var called = false;
        var middleware = new RequestTelemetryMiddleware(_ =>
        {
            called = true;
            logger.Information("inside the request");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(called, Is.True);
            Assert.That(context.Response.Headers[RequestTelemetryMiddleware.TraceResponseHeader].ToString(), Does.StartWith($"00-{activity.TraceId.ToHexString()}-"));
            Assert.That(sink.Events.Single().Properties[RequestTelemetryMiddleware.ClientIpProperty].ToString(), Is.EqualTo("\"203.0.113.7\""));
            // Serilog stamps the trace natively; this is what the OTLP sink sends as trace_id.
            Assert.That(sink.Events.Single().TraceId, Is.EqualTo(activity.TraceId));
        });
    }

    [Test]
    public async Task Middleware_WithoutActivity_SetsNoHeader()
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            var context = new DefaultHttpContext();
            await new RequestTelemetryMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
            Assert.That(context.Response.Headers.ContainsKey(RequestTelemetryMiddleware.TraceResponseHeader), Is.False);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [TestCase("/Api/Voters", 200, false, LogEventLevel.Information)]
    [TestCase("/health", 200, false, LogEventLevel.Verbose)]
    [TestCase("/health/ready", 503, false, LogEventLevel.Error)]
    [TestCase("/Api/Voters", 500, false, LogEventLevel.Error)]
    [TestCase("/Api/Voters", 200, true, LogEventLevel.Error)]
    public void GetLevel(string path, int status, bool exception, LogEventLevel expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;

        Assert.That(RequestTelemetryExtensions.GetLevel(context, 1.0, exception ? new InvalidOperationException() : null), Is.EqualTo(expected));
    }

    [TestCase("/healthz", LogEventLevel.Verbose)]
    [TestCase("/healthz/ready", LogEventLevel.Verbose)]
    [TestCase("/health", LogEventLevel.Information)]
    public void GetLevel_FollowsConfiguredHealthPaths(string path, LogEventLevel expected)
    {
        using var services = new ServiceCollection()
            .AddSingleton(new HealthCheckPaths("/healthz", "/healthz/live", "/healthz/ready"))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;

        Assert.That(RequestTelemetryExtensions.GetLevel(context, 1.0, null), Is.EqualTo(expected));
    }

    [Test]
    public void EnrichDiagnosticContext_SetsRequestDetails()
    {
        var diagnostics = Substitute.For<IDiagnosticContext>();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("headhunter.gurnt.com");
        context.Request.Headers.UserAgent = "curl/8";
        context.SetEndpoint(new Endpoint(null, null, "ApiController.Voters"));

        RequestTelemetryExtensions.EnrichDiagnosticContext(diagnostics, context);

        diagnostics.Received().Set("ClientIp", "203.0.113.7");
        diagnostics.Received().Set("RequestHost", "headhunter.gurnt.com");
        diagnostics.Received().Set("RequestScheme", "https");
        diagnostics.Received().Set("UserAgent", "curl/8");
        diagnostics.Received().Set("EndpointName", "ApiController.Voters");
    }

    [Test]
    public void EnrichDiagnosticContext_NoEndpoint_NoEndpointName()
    {
        var diagnostics = Substitute.For<IDiagnosticContext>();
        RequestTelemetryExtensions.EnrichDiagnosticContext(diagnostics, new DefaultHttpContext());

        diagnostics.DidNotReceive().Set("EndpointName", Arg.Any<string>());
    }

    [Test]
    public async Task Pipeline_LogsOneLinePerRequest_WithTheRealClient_AndSkipsHealth()
    {
        var sink = new CollectingSink();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["ForwardedHeaders:KnownIPNetworks:0"] = "10.42.0.0/16";
        builder.Configuration["ForwardedHeaders:ForwardedForHeaderName"] = "X-Real-IP";
        builder.Services.ConfigureAppForwardedHeaders(builder.Configuration);
        // Only the request log. UseSerilogRequestLogging writes to the static logger, as in Program.
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByIncludingOnly(Serilog.Filters.Matching.FromSource("Serilog.AspNetCore.RequestLoggingMiddleware"))
            .WriteTo.Sink(sink)
            .CreateLogger();
        try
        {
            builder.Services.AddSerilog();
            var telemetry = TelemetryOptions.FromConfiguration(builder.Configuration, builder.Environment);
            builder.Services.AddMetricsAndTracing(telemetry, builder.Configuration);

            await using var app = builder.Build();
            app.UseForwardedHeaders();
            app.UseRequestTelemetry();
            app.MapGet("/Api/hello", () => "hi");
            app.MapGet("/health", () => "Healthy");
            await app.StartAsync();

            var server = app.GetTestServer();
            var response = await server.SendAsync(c =>
            {
                c.Request.Path = "/Api/hello";
                c.Connection.RemoteIpAddress = IPAddress.Parse("10.42.1.245");
                c.Request.Headers["X-Real-IP"] = "68.41.224.97";
            });
            await server.SendAsync(c => c.Request.Path = "/health");

            // TestServer hands back the response before the middleware unwinds and writes its line.
            for (var i = 0; i < 100 && sink.Events.Count == 0; i++)
                await Task.Delay(50);
            await Task.Delay(100); // room for a wrongly logged /health line to show up too

            var requestLog = sink.Events.Single();
            Assert.Multiple(() =>
            {
                Assert.That(response.Response.Headers.ContainsKey(RequestTelemetryMiddleware.TraceResponseHeader), Is.True);
                Assert.That(requestLog.Properties["RequestPath"].ToString(), Is.EqualTo("\"/Api/hello\""));
                Assert.That(requestLog.Properties["StatusCode"].ToString(), Is.EqualTo("200"));
                Assert.That(requestLog.Properties["ClientIp"].ToString(), Is.EqualTo("\"68.41.224.97\""));
                Assert.That(requestLog.TraceId?.ToHexString(), Is.EqualTo(response.Response.Headers["traceresponse"].ToString().Split('-')[1]));
            });
        }
        finally
        {
            (Log.Logger as IDisposable)?.Dispose();
            Log.Logger = previous;
        }
    }
}
