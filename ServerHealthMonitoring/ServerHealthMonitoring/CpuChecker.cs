using System;
using System.Diagnostics;

public static class CpuChecker
{
    public static string? Check()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "[SKIP] CPU check only available on Windows";

            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            System.Threading.Thread.Sleep(1000);
            float value = cpuCounter.NextValue();

            return value > 90
                ? $"[WARNING] High CPU usage: {value}%"
                : null;
        }
        catch (Exception ex)
        {
            return $"[CPU ERROR] {ex.Message}";
        }
    }
}