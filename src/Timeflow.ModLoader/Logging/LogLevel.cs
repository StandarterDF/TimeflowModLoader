using System;

namespace Timeflow.ModLoader.Logging
{
    /// <summary>Severity of a log message. Ordered from most to least verbose.</summary>
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5,
        None = 6
    }

    internal static class LogLevelExtensions
    {
        public static string Tag(this LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace: return "TRACE";
                case LogLevel.Debug: return "DEBUG";
                case LogLevel.Info: return "INFO ";
                case LogLevel.Warning: return "WARN ";
                case LogLevel.Error: return "ERROR";
                case LogLevel.Fatal: return "FATAL";
                default: return "-----";
            }
        }
    }
}
