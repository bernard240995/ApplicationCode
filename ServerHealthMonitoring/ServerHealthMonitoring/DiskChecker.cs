using System;
using System.IO;
using System.Linq;

public static class DiskChecker
{
    public static string? Check()
    {
        try
        {
            var errors = DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
                .Select(drive => {
                    double freeGB = drive.TotalFreeSpace / (1024.0 * 1024 * 1024);
                    return freeGB < 5
                        ? $"[WARNING] Low disk space on {drive.Name}: {freeGB:F2} GB free"
                        : null;
                })
                .Where(msg => msg != null)
                .ToList();

            return errors.Any() ? string.Join("\n", errors) : null;
        }
        catch (Exception ex)
        {
            return $"[DISK ERROR] {ex.Message}";
        }
    }
}