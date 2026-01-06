using Avalonia.Logging;

namespace Gantry;

class SerilogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area)
    {
        return Serilog.Log.IsEnabled(level.ToSerilog());
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        string context = source?.GetType().Name ?? area;
        Serilog.Log
            .ForContext("SourceContext", context)
            .Write(level.ToSerilog(), messageTemplate);
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        string context = source?.GetType().Name ?? area;
        Serilog.Log
            .ForContext("SourceContext", context)
            .Write(level.ToSerilog(), messageTemplate, propertyValues);
    }
}

file static class Extensions
{
    public static Serilog.Events.LogEventLevel ToSerilog(this LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => Serilog.Events.LogEventLevel.Verbose,
            LogEventLevel.Debug => Serilog.Events.LogEventLevel.Debug,
            LogEventLevel.Information => Serilog.Events.LogEventLevel.Information,
            LogEventLevel.Warning => Serilog.Events.LogEventLevel.Warning,
            LogEventLevel.Error => Serilog.Events.LogEventLevel.Error,
            LogEventLevel.Fatal => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Debug
        };
}