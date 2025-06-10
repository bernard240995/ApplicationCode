using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CloudflareStatusChecker.Models;
using CloudflareStatusChecker.Converters;

namespace CloudflareStatusChecker.Services
{
    public class IncidentService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;

        public IncidentService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CloudflareStatusChecker/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new DateTimeConverter() }
            };
        }

        public async Task<IncidentResponse> FetchIncidents(string apiUrl)
        {
            try
            {
                ConsoleHelper.WriteColored($"Fetching incidents from: {apiUrl}", ConsoleColor.Yellow);
                var response = await _httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                ConsoleHelper.WriteColored("\n=== RAW API RESPONSE ===", ConsoleColor.DarkYellow);
                ConsoleHelper.WriteColored(content.Length > 500 ? content.Substring(0, 500) + "..." : content, ConsoleColor.Gray);

                var result = JsonSerializer.Deserialize<IncidentResponse>(content, _jsonOptions);

                if (result == null || result.Incidents == null)
                {
                    throw new JsonException("Failed to deserialize incident response or no incidents found");
                }

                LogParsedData(result);
                return result;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteColored($"Error fetching incidents: {ex.Message}", ConsoleColor.Red);
                throw;
            }
        }

        public List<Incident> FilterBillingIncidents(List<Incident> incidents)
        {
            if (incidents == null || !incidents.Any())
                return new List<Incident>();

            return incidents
                .Where(i =>
                    (i.Name?.Contains("billing", StringComparison.OrdinalIgnoreCase) == true) ||
                    (i.IncidentUpdates?.Any(u =>
                        u.Body?.Contains("billing", StringComparison.OrdinalIgnoreCase) == true) == true) ||
                    (i.AffectedComponents?.Any(c =>
                        c.Name?.Contains("billing", StringComparison.OrdinalIgnoreCase) == true) == true)
                )
                .ToList(); // Corrected ToList() placement
        }

        public IncidentResponse CombineResponses(IncidentResponse mainResponse, IncidentResponse unresolvedResponse)
        {
            var allIncidents = new List<Incident>();
            if (mainResponse?.Incidents != null) allIncidents.AddRange(mainResponse.Incidents);
            if (unresolvedResponse?.Incidents != null) allIncidents.AddRange(unresolvedResponse.Incidents);

            return new IncidentResponse
            {
                Page = mainResponse?.Page ?? unresolvedResponse?.Page ?? new PageInfo(),
                Incidents = allIncidents
                    .GroupBy(i => i.Id)
                    .Select(g => g.First())
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList()
            };
        }

        private void LogParsedData(IncidentResponse response)
        {
            if (response?.Incidents == null || !response.Incidents.Any())
            {
                ConsoleHelper.WriteColored("No incidents found in the response", ConsoleColor.Yellow);
                return;
            }

            ConsoleHelper.WriteColored("\n=== PARSED INCIDENT DATA ===", ConsoleColor.DarkYellow);
            ConsoleHelper.WriteColored($"Total Incidents: {response.Incidents.Count}", ConsoleColor.Gray);

            foreach (var incident in response.Incidents.Take(3))
            {
                ConsoleHelper.WriteColored(
                    $"\nID: {incident.Id}\n" +
                    $"Name: {incident.Name}\n" +
                    $"Status: {incident.Status}\n" +
                    $"Impact: {incident.Impact}\n" +
                    $"Created: {incident.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                    $"Updated: {incident.UpdatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                    $"Resolved: {(incident.ResolvedAt.HasValue ? incident.ResolvedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "N/A")}\n" +
                    $"Shortlink: {incident.Shortlink ?? "N/A"}\n" +
                    $"Components: {(incident.AffectedComponents != null ? string.Join(", ", incident.AffectedComponents.Select(c => c.Name)) : "N/A")}\n" +
                    $"Updates: {incident.IncidentUpdates?.Count ?? 0}",
                    ConsoleColor.Gray);

                if (incident.IncidentUpdates != null && incident.IncidentUpdates.Any())
                {
                    var latestUpdate = incident.IncidentUpdates.OrderByDescending(u => u.CreatedAt).First();
                    ConsoleHelper.WriteColored(
                        $"Latest Update:\n" +
                        $"Status: {latestUpdate.Status}\n" +
                        $"Time: {latestUpdate.CreatedAt:dd/MM/yyyy HH:mm:ss}\n" +
                        $"Body: {(latestUpdate.Body?.Length > 50 ? latestUpdate.Body.Substring(0, 50) + "..." : latestUpdate.Body)}",
                        ConsoleColor.DarkGray);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}