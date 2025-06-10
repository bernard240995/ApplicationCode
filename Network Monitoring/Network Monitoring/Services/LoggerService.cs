using System;
using System.IO;

namespace AdvancedNetworkMonitor.Services
{
    public static class LoggerService
    {
        private static readonly object _lock = new object();
        private static readonly string _logFilePath = "network_monitor.log";

        public static void Log(string message, LogLevel level = LogLevel.INFO)
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"{timestamp} [{level}] {message}";

                Console.ForegroundColor = level switch
                {
                    LogLevel.ERROR => ConsoleColor.Red,
                    LogLevel.WARNING => ConsoleColor.Yellow,
                    LogLevel.SUCCESS => ConsoleColor.Green,
                    LogLevel.DEBUG => ConsoleColor.Cyan,
                    _ => ConsoleColor.White
                };

                Console.WriteLine(logEntry);
                Console.ResetColor();

                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to write to log: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        public enum LogLevel
        {
            INFO,
            SUCCESS,
            WARNING,
            ERROR,
            DEBUG
        }
    }
}