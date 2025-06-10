using System;
using System.Management;
using System.Linq;

public static class NetworkChecker
{
    public static string Check()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "[SKIP] Network check only supported on Windows";

            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled = TRUE");

            var errors = searcher.Get()
                .Cast<ManagementObject>()
                .Select(adapter => {
                    string name = adapter["Name"]?.ToString() ?? "Unknown";
                    string status = adapter["NetConnectionStatus"]?.ToString() ?? "0";
                    return status != "2"
                        ? $"[WARNING] Network issue: {name} (Status: {status})"
                        : null;
                })
                .Where(msg => msg != null)
                .ToList();

            return errors.Any() ? string.Join("\n", errors) : null;
        }
        catch (Exception ex)
        {
            return $"[NETWORK ERROR] {ex.Message}";
        }
    }
}