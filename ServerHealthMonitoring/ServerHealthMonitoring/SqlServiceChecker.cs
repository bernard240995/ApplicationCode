using System;
using System.Linq;
using System.ServiceProcess;

public static class SqlServiceChecker
{
    public static string Check()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "[SKIP] SQL Service check only available on Windows";

            string[] sqlServices = {
                "MSSQLSERVER",          
                "SQLSERVERAGENT",      
                "MSSQL$",              
                "SQLAgent$",            
                "SQLBrowser",           
                "SQLWriter",           
                "MsDtsServer",         
                "SQLTELEMETRY",         
                "SSISTELEMETRY",       
                "MSSQL$SQLEXPRESS",    
                "SQLAgent$SQLEXPRESS", 
                "MSSQLLaunchpad",       
                "SQLSERVERLAUNCHER"    
            };

            var services = ServiceController.GetServices()
                .Where(s => sqlServices.Any(sqlName =>
                    s.ServiceName.StartsWith(sqlName, StringComparison.OrdinalIgnoreCase) ||
                    s.DisplayName.Contains("SQL Server", StringComparison.OrdinalIgnoreCase) ||
                    s.DisplayName.Contains("SQL Express", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!services.Any())
            {
                return "[INFO] No SQL Services found - This server doesn't require SQL Services";
            }

            var statusMessages = new System.Collections.Generic.List<string>();
            var actionMessages = new System.Collections.Generic.List<string>();

            foreach (var service in services)
            {
                try
                {
                    service.Refresh();

                    
                    if (service.StartType == ServiceStartMode.Disabled)
                    {
                        statusMessages.Add($"[INFO] SQL Service {service.DisplayName} ({service.ServiceName}) is disabled (not started)");
                        continue;
                    }

                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        try
                        {
                            
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                            if (service.Status == ServiceControllerStatus.Running)
                            {
                                actionMessages.Add($"[ACTION] SQL Service {service.DisplayName} ({service.ServiceName}) was stopped and has been restarted");
                            }
                            else
                            {
                                statusMessages.Add($"[WARNING] Failed to start SQL Service {service.DisplayName} ({service.ServiceName}) - Current status: {service.Status}");
                            }
                        }
                        catch (Exception startEx)
                        {
                            statusMessages.Add($"[SQL SERVICE ERROR] Failed to start {service.DisplayName}: {startEx.Message}");
                        }
                    }
                    else
                    {
                        statusMessages.Add($"[INFO] SQL Service {service.DisplayName} ({service.ServiceName}) is running normally");
                    }
                }
                catch (Exception ex)
                {
                    statusMessages.Add($"[SQL SERVICE ERROR] {service.DisplayName}: {ex.Message}");
                }
            }

          
            var allMessages = actionMessages.Concat(statusMessages).ToList();

            if (actionMessages.Any())
            {
                return string.Join("\n", allMessages);
            }
            else if (statusMessages.Any(m => m.Contains("[WARNING]") || m.Contains("[ERROR]")))
            {
                return string.Join("\n", allMessages);
            }
            else
            {
                return "[INFO] All SQL Services running normally\n" + string.Join("\n", allMessages.Where(m => m.StartsWith("[INFO]")));
            }
        }
        catch (Exception ex)
        {
            return $"[SQL CHECK ERROR] {ex.Message}";
        }
    }
}