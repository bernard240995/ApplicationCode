using System;
using System.Diagnostics;
using System.ServiceProcess;

public static class HpIloChecker
{
    public static string Check()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "[SKIP] HP iLO check only available on Windows";

            
            var iloServices = ServiceController.GetServices()
                .Where(s => s.ServiceName.Contains("iLO", StringComparison.OrdinalIgnoreCase) ||
                             s.DisplayName.Contains("iLO", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!iloServices.Any())
            {
                return "[INFO] No HP iLO services found on this machine";
            }

            
            var hpServices = ServiceController.GetServices()
                .Where(s => s.ServiceName.Contains("HP", StringComparison.OrdinalIgnoreCase) ||
                           s.DisplayName.Contains("HP", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var errors = new System.Collections.Generic.List<string>();

            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "hplog",
                    Arguments = "-s",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (output.Contains("Critical") || output.Contains("Failed"))
                    {
                        errors.Add("[WARNING] HP iLO hardware issues detected:\n" + output);
                    }
                }
            }
            catch
            {
                
            }

            
            foreach (var service in iloServices.Concat(hpServices))
            {
                try
                {
                    service.Refresh();
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        errors.Add($"[WARNING] HP Service {service.DisplayName} is not running (Status: {service.Status})");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"[HP SERVICE ERROR] {service.DisplayName}: {ex.Message}");
                }
            }

            return errors.Any()
                ? string.Join("\n", errors)
                : "[INFO] HP iLO services detected and running normally";
        }
        catch (Exception ex)
        {
            return $"[HP ILO ERROR] {ex.Message}";
        }
    }
}

