namespace Adeni.Infrastructure.Tests.Calendar;
using Adeni.Application.Calendar;
using Adeni.Infrastructure.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
public sealed class CalendarLoggingTests
{
    private sealed class Sink : ILogEventSink { public List<LogEvent> Events=[]; public void Emit(LogEvent logEvent)=>Events.Add(logEvent); }
    [Fact]
    public void OAuthHostingDiagnostics_AreExcluded_AndCredentialsCannotBeDestructured()
    {
        var sink=new Sink();using var log=new LoggerConfiguration().Destructure.With<PiiDestructuringPolicy>().Filter.ByExcluding(CalendarOAuthLogFilter.ShouldExclude).WriteTo.Sink(sink).CreateLogger();
        log.ForContext("SourceContext","Microsoft.AspNetCore.Hosting.Diagnostics").Information("Request {Path}{QueryString}","/api/v1/calendar/oauth/google/callback","?code=secret-code");
        Assert.Empty(sink.Events);
        log.Information("Credentials {@Grant}",new CalendarGrant{RefreshToken="sensitive-refresh",AccessToken="sensitive-access"});
        log.Information("Launch {@Launch}",new CalendarOAuthLaunch("https://example.test?state=secret-state","secret-cookie"));
        log.ForContext("SourceContext","Microsoft.AspNetCore.Hosting.Diagnostics").Information("Request {Path}","/api/v1/businesses");
        Assert.Equal(3,sink.Events.Count);var rendered=string.Join('\n',sink.Events.Select(x=>x.RenderMessage()));Assert.DoesNotContain("sensitive-",rendered);Assert.DoesNotContain("secret-",rendered);
    }
}
