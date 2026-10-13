namespace Timeflow.ModLoader.Logging
{
    /// <summary>
    /// Per-mod logger. Prefer this over <see cref="ModLog"/> so that every line carries the
    /// originating mod id, which makes troubleshooting multi-mod setups much easier.
    /// </summary>
    public sealed class ModLogger
    {
        public string Source { get; }

        public ModLogger(string source)
        {
            Source = string.IsNullOrEmpty(source) ? "Mod" : source;
        }

        public void Trace(object message) => Write(LogLevel.Trace, message);
        public void Debug(object message) => Write(LogLevel.Debug, message);
        public void Info(object message) => Write(LogLevel.Info, message);
        public void Warn(object message) => Write(LogLevel.Warning, message);
        public void Error(object message) => Write(LogLevel.Error, message);
        public void Fatal(object message) => Write(LogLevel.Fatal, message);

        public void Write(LogLevel level, object message)
            => ModLog.WriteLine(level, Source, message?.ToString() ?? "null");
    }
}
