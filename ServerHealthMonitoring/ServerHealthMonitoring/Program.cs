using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Text;

[assembly: SupportedOSPlatform("windows")]

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Server Health Monitor Started");
        Logger.Log("Monitoring started.", ConsoleColor.Green);

        var emailNotifier = new EmailNotifier(new EmailConfig
        {
            SmtpServer = "smtp.gmail.com",
            SmtpPort = 587,
            Username = "bernardwotherspoon55@gmail.com",
            Password = "bsiw jwvv yqay nrpj",
            FromAddress = "servermonitoring@testdomain.co.uk",
            SuccessRecipients = new[] { "bernard3477286@outlook.com" },
            ErrorRecipients = new[] { "bernard3477286@outlook.com" }
        });

        string[] criticalServices = { "w32time", "WinDefend" };
        var serviceChecker = new ServiceChecker(criticalServices);

        DateTime lastStatusReportTime = DateTime.MinValue;
        const int statusReportIntervalMinutes = 30;

        while (true)
        {
            var checkResults = new StringBuilder();
            var errorMessages = new System.Collections.Concurrent.ConcurrentBag<string>();

            try
            {

                string serviceStatus = serviceChecker.CheckAndRestart();
                checkResults.AppendLine("=== Critical Services ===");
                if (string.IsNullOrEmpty(serviceStatus))
                {
                    checkResults.AppendLine("<span style='color:green'>All critical services running normally</span>");
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{serviceStatus}</span>");
                    errorMessages.Add(serviceStatus);
                }


                checkResults.AppendLine("\n=== CPU Status ===");
                string cpuStatus = CpuChecker.Check();
                if (string.IsNullOrEmpty(cpuStatus))
                {
                    checkResults.AppendLine("<span style='color:green'>CPU usage normal</span>");
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{cpuStatus}</span>");
                    errorMessages.Add(cpuStatus);
                }

                checkResults.AppendLine("\n=== RAM Status ===");
                string ramStatus = RamChecker.Check();
                if (string.IsNullOrEmpty(ramStatus))
                {
                    checkResults.AppendLine("<span style='color:green'>RAM usage normal</span>");
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{ramStatus}</span>");
                    errorMessages.Add(ramStatus);
                }

                string diskStatus = DiskChecker.Check();
                checkResults.AppendLine("\n=== Disk Status ===");
                if (string.IsNullOrEmpty(diskStatus))
                {
                    checkResults.AppendLine("<span style='color:green'>Disk space adequate</span>");
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{diskStatus}</span>");
                    errorMessages.Add(diskStatus);
                }

                
                string networkStatus = NetworkChecker.Check();
                checkResults.AppendLine("\n=== Network Status ===");
                if (string.IsNullOrEmpty(networkStatus))
                {
                    checkResults.AppendLine("<span style='color:green'>Network adapters OK</span>");
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{networkStatus}</span>");
                    errorMessages.Add(networkStatus);
                }

              
                string fileShareStatus = FileShareChecker.Check();
                checkResults.AppendLine("\n=== File Shares Status ===");
                if (fileShareStatus.Contains("[WARNING]") || fileShareStatus.Contains("[ERROR]"))
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{fileShareStatus}</span>");
                    errorMessages.Add(fileShareStatus);
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:green'>{fileShareStatus}</span>");
                }


                string sqlStatus = SqlServiceChecker.Check();
                checkResults.AppendLine("\n=== SQL Services Status ===");
                if (sqlStatus.Contains("[WARNING]") || sqlStatus.Contains("[ERROR]"))
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{sqlStatus}</span>");
                    errorMessages.Add(sqlStatus);
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:green'>{sqlStatus}</span>");
                }


                string hpIloStatus = HpIloChecker.Check();
                checkResults.AppendLine("\n=== HP iLO Status ===");
                if (hpIloStatus.Contains("[WARNING]") || hpIloStatus.Contains("[ERROR]"))
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{hpIloStatus}</span>");
                    errorMessages.Add(hpIloStatus);
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:green'>{hpIloStatus}</span>");
                }


                string dellStatus = DellServerChecker.Check();
                checkResults.AppendLine("\n=== Dell Server Status ===");
                if (dellStatus.Contains("[WARNING]") || dellStatus.Contains("[ERROR]"))
                {
                    checkResults.AppendLine($"<span style='color:red;font-weight:bold'>{dellStatus}</span>");
                    errorMessages.Add(dellStatus);
                }
                else
                {
                    checkResults.AppendLine($"<span style='color:green'>{dellStatus}</span>");
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"<span style='color:red;font-weight:bold'>[MONITORING ERROR] {ex.Message}</span>";
                errorMessages.Add(errorMsg);
                checkResults.AppendLine($"\nMonitoring Error: {errorMsg}");
                Logger.Log($"[ERROR] {ex.Message}", ConsoleColor.Red);
            }

            bool isTimeForStatusReport = (DateTime.Now - lastStatusReportTime).TotalMinutes >= statusReportIntervalMinutes;
            bool shouldSendStatusReport = isTimeForStatusReport || !errorMessages.IsEmpty;

            if (shouldSendStatusReport)
            {
                emailNotifier.SendNotification(
                    isError: !errorMessages.IsEmpty,
                    subject: errorMessages.IsEmpty
                        ? "Server Health - All Systems OK"
                        : "SERVER ALERT - Issues Detected",
                    body: checkResults.ToString()
                );

                if (isTimeForStatusReport)
                {
                    lastStatusReportTime = DateTime.Now;
                }
            }

            Thread.Sleep(0);
        }
    }
}