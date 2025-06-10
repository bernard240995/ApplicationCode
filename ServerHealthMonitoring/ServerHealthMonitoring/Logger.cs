using System;
using System.IO;

public static class Logger
{
    private static readonly string logFilePath = $"log_{DateTime.Now:yyyyMMddHHmmss}.txt";

    public static void Log(string message, ConsoleColor color)
    {
        string timestamped = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        Console.ForegroundColor = color;
        Console.WriteLine(timestamped);
        Console.ResetColor();
        File.AppendAllText(logFilePath, timestamped + Environment.NewLine);
    }
}

