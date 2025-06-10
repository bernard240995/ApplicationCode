using System;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Linq;

public class ServiceChecker
{
    private readonly string[] servicesToCheck;

    public ServiceChecker(string[] services) => servicesToCheck = services;

    [SupportedOSPlatform("windows")]
    public string CheckAndRestart()
    {
        var errors = servicesToCheck
            .Select(serviceName => {
                try
                {
                    using var sc = new ServiceController(serviceName);
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        return $"[WARNING] {serviceName} was restarted";
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    return $"[SERVICE ERROR] {serviceName}: {ex.Message}";
                }
            })
            .Where(msg => msg != null)
            .ToList();

        return errors.Any() ? string.Join("\n", errors) : null;
    }
}