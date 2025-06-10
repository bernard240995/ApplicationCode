using System;
using System.Diagnostics;
using System.ServiceProcess;

public static class DellServerChecker
{
    public static string Check()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "[SKIP] Dell server check only available on Windows";

            
            var dellServices = ServiceController.GetServices()
                .Where(s => s.ServiceName.Contains("Dell", StringComparison.OrdinalIgnoreCase) ||
                             s.DisplayName.Contains("Dell", StringComparison.OrdinalIgnoreCase) ||
                             s.ServiceName.Contains("OMSA", StringComparison.OrdinalIgnoreCase) ||
                             s.DisplayName.Contains("OpenManage", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!dellServices.Any())
            {
                return "[INFO] No Dell server management services found on this machine";
            }

            var errors = new System.Collections.Generic.List<string>();

            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "omreport",
                    Arguments = "chassis",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (output.Contains("Critical") || output.Contains("Failed") || output.Contains("Non-Critical"))
                    {
                        errors.Add("[WARNING] Dell hardware issues detected:\n" + output);
                    }
                }
            }
            catch
            {
                
            }

            
            foreach (var service in dellServices)
            {
                try
                {
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        errors.Add($"[WARNING] Dell Service {service.DisplayName} is not running (Status: {service.Status})");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"[DELL SERVICE ERROR] {service.DisplayName}: {ex.Message}");
                }
            }

            return errors.Any()
                ? string.Join("\n", errors)
                : "[INFO] Dell server management services detected and running normally";
        }
        catch (Exception ex)
        {
            return $"[DELL SERVER ERROR] {ex.Message}";
        }
    }
}
