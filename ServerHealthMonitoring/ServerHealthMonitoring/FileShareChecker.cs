using System;
using System.ServiceProcess;
using System.IO;
using System.Management;
using System.Collections.Generic;

public static class FileShareChecker
{
    public static string Check()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return "[SKIP] File Share check only available on Windows";

            using var serverService = new ServiceController("Server");
            serverService.Refresh();

            if (serverService.Status != ServiceControllerStatus.Running)
            {
                try
                {
                    serverService.Start();
                    serverService.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    return "[ACTION] File Share service (Server) was stopped and has been restarted";
                }
                catch (Exception ex)
                {
                    return $"[ERROR] Failed to restart File Share service (Server): {ex.Message}";
                }
            }

            List<string> inaccessibleShares = new List<string>();

            
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Fixed)
                {
                    string share = drive.Name.Substring(0, 1) + "$";
                    string path = $@"\\{Environment.MachineName}\{share}";
                    try
                    {
                        if (!Directory.Exists(path))
                        {
                            inaccessibleShares.Add(share);
                        }
                    }
                    catch
                    {
                        inaccessibleShares.Add(share);
                    }
                }
            }

            try
            {
                var searcher = new ManagementObjectSearcher("SELECT Name, Path FROM Win32_Share");
                foreach (ManagementObject share in searcher.Get())
                {
                    string shareName = share["Name"].ToString();

                    
                    if (shareName.Equals("IPC$", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string path = $@"\\{Environment.MachineName}\{shareName}";

                    try
                    {
                        if (!Directory.Exists(path))
                        {
                            inaccessibleShares.Add(shareName);
                        }
                    }
                    catch
                    {
                        inaccessibleShares.Add(shareName);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[ERROR] Failed to enumerate shares: {ex.Message}";
            }

            if (inaccessibleShares.Count > 0)
            {
                return $"[WARNING] The following shares are not accessible: {string.Join(", ", inaccessibleShares)}";
            }

            return "[INFO] All available shares are accessible";
        }
        catch (Exception ex)
        {
            return $"[FILE SHARE ERROR] {ex.Message}";
        }
    }
}