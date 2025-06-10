using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CloudflareStatusChecker.Models;
using CloudflareStatusChecker.Services;

[assembly: Obfuscation(Feature = "renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "apply to type *: renaming", Exclude = true)]

namespace CloudflareStatusChecker
{
    public static class Program
    {
        public static async Task Main()
        {
            try
            {
                ConsoleHelper.WriteColored("Loading configuration...", ConsoleColor.Yellow);
                var settings = ConfigHelper.LoadSettings() ?? throw new InvalidOperationException("Configuration could not be loaded");
                ValidateSettings(settings);

                string outputDirectory = AppDomain.CurrentDomain.BaseDirectory;
                ConsoleHelper.WriteColored($"Application running from: {outputDirectory}", ConsoleColor.Cyan);
                Directory.CreateDirectory(outputDirectory);

                ConsoleHelper.WriteColored("=== Cloudflare Incident Reporter ===", ConsoleColor.Cyan);
                ConsoleHelper.WriteColored($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Yellow);

                var incidentService = new IncidentService();
                var excelService = new ExcelReportService();
                var emailService = new EmailService();

                ConsoleHelper.WriteColored("Fetching main incidents...", ConsoleColor.Yellow);
                var mainResponse = await incidentService.FetchIncidents(settings.ApiUrl);

                ConsoleHelper.WriteColored("Fetching unresolved incidents...", ConsoleColor.Yellow);
                var unresolvedResponse = await incidentService.FetchIncidents(settings.UnresolvedApiUrl);

                ConsoleHelper.WriteColored("Combining incident data...", ConsoleColor.Yellow);
                var combinedResponse = incidentService.CombineResponses(mainResponse, unresolvedResponse) ??
                                       throw new InvalidOperationException("Failed to combine responses");

                var allIncidents = combinedResponse.Incidents ?? new List<Incident>();
                var activeIncidents = allIncidents.Where(i => i?.Status?.Equals("investigating", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
                var resolvedIncidents = allIncidents.Where(i => i != null && !(i.Status?.Equals("investigating", StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

                ConsoleHelper.WriteColored($"{activeIncidents.Count} active incidents found.", ConsoleColor.Green);
                ConsoleHelper.WriteColored($"{resolvedIncidents.Count} resolved incidents found.", ConsoleColor.Green);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (resolvedIncidents.Count > 0)
                {
                    await ProcessIncidentReport(
                        excelService,
                        emailService,
                        settings,
                        combinedResponse.Page,
                        resolvedIncidents,
                        $"Cloudflare Resolved Incidents - {timestamp}",
                        "resolved",
                        outputDirectory);
                }

                if (activeIncidents.Count > 0)
                {
                    await ProcessIncidentReport(
                        excelService,
                        emailService,
                        settings,
                        combinedResponse.Page,
                        activeIncidents,
                        $"Cloudflare Active Incidents - {timestamp}",
                        "active",
                        outputDirectory);
                }

                ConsoleHelper.WriteColored("Operation completed successfully!", ConsoleColor.Green);
                ConsoleHelper.WriteColored($"Finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Yellow);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteColored($"FATAL ERROR: {ex.Message}", ConsoleColor.Red);
                ConsoleHelper.WriteColored($"Stack Trace: {ex.StackTrace}", ConsoleColor.DarkRed);
                if (ex.InnerException != null)
                    ConsoleHelper.WriteColored($"INNER EXCEPTION: {ex.InnerException.Message}", ConsoleColor.DarkRed);

                Environment.Exit(1);
            }
        }

        private static void ValidateSettings(Settings settings)
        {
            void Check(string? value, string name)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ConfigurationErrorsException($"{name} is not configured");
            }

            Check(settings.Smtp?.Server, "SMTP Server");
            Check(settings.Smtp?.Username, "SMTP Username");
            Check(settings.Smtp?.Password, "SMTP Password");
            Check(settings.EmailFrom, "Email From address");
            Check(settings.EmailTo, "Email To address");
        }

        private static async Task ProcessIncidentReport(
            ExcelReportService excelService,
            EmailService emailService,
            Settings settings,
            PageInfo page,
            List<Incident> incidents,
            string subject,
            string reportType,
            string outputDirectory)
        {
            try
            {
                ConsoleHelper.WriteColored($"Generating {reportType} incidents report...", ConsoleColor.Yellow);

                var response = new IncidentResponse { Page = page, Incidents = incidents };
                using var reportStream = excelService.GenerateReport(response) ?? throw new InvalidOperationException("Failed to generate report");

                Exception? lastError = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        ConsoleHelper.WriteColored($"Sending {reportType} incidents email (attempt {attempt})...", ConsoleColor.Yellow);
                        reportStream.Position = 0;
                        await emailService.SendEmailWithAttachmentAsync(reportStream, settings, subject);
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        ConsoleHelper.WriteColored($"Attempt {attempt} failed: {ex.Message}", ConsoleColor.Yellow);
                        if (attempt < 3) await Task.Delay(2000 * attempt);
                    }
                }

                throw lastError ?? new ApplicationException($"Failed to send {reportType} incidents email after 3 attempts");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteColored($"Failed to process {reportType} incidents: {ex.Message}", ConsoleColor.Red);
                await SaveFallbackReport(excelService, page, incidents, reportType, outputDirectory);
                throw;
            }
        }

        private static async Task SaveFallbackReport(ExcelReportService excelService, PageInfo page, IEnumerable<Incident> incidents, string reportType, string outputDirectory)
        {
            try
            {
                var fallbackPath = Path.Combine(outputDirectory, $"Cloudflare_{reportType}_fallback_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                ConsoleHelper.WriteColored($"Saving fallback to: {fallbackPath}", ConsoleColor.DarkYellow);

                var response = new IncidentResponse { Page = page, Incidents = incidents.ToList() };
                using var reportStream = excelService.GenerateReport(response);
                using var fileStream = File.Create(fallbackPath);
                reportStream.Position = 0;
                await reportStream.CopyToAsync(fileStream);

                ConsoleHelper.WriteColored($"Saved fallback report to: {fallbackPath}", ConsoleColor.Yellow);
            }
            catch (Exception saveEx)
            {
                ConsoleHelper.WriteColored($"Failed to save fallback report: {saveEx.Message}", ConsoleColor.Red);
            }
        }
    }
}