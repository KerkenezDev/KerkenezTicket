using System;
using System.Collections.Generic;

namespace KerkenezTicket.Services
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "INFO"; // INFO, SUCCESS, WARN, ERROR
        public string Category { get; set; } = "App";
        public string Message { get; set; } = "";

        public override string ToString() =>
            $"[{Timestamp:HH:mm:ss}] [{Level}] [{Category}] {Message}";
    }

    public static class LogService
    {
        private static readonly List<LogEntry> _logs = new List<LogEntry>();
        private static readonly object _lock = new object();
        private const int MaxEntries = 1000;

        public static event Action<LogEntry>? LogAdded;

        public static void Log(string level, string category, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level.ToUpperInvariant(),
                Category = category,
                Message = message
            };

            lock (_lock)
            {
                _logs.Add(entry);
                if (_logs.Count > MaxEntries)
                {
                    _logs.RemoveAt(0);
                }
            }

            LogAdded?.Invoke(entry);
        }

        public static void Info(string category, string message) => Log("INFO", category, message);
        public static void Success(string category, string message) => Log("SUCCESS", category, message);
        public static void Warn(string category, string message) => Log("WARN", category, message);
        public static void Error(string category, string message) => Log("ERROR", category, message);

        public static List<LogEntry> GetRecentLogs()
        {
            lock (_lock)
            {
                return new List<LogEntry>(_logs);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _logs.Clear();
            }
        }
    }
}
