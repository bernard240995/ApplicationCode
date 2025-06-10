using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CloudflareStatusChecker.Models
{
    public class IncidentResponse
    {
        public PageInfo Page { get; set; } = new PageInfo();
        public List<Incident> Incidents { get; set; } = new List<Incident>();
    }

    public class PageInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class Incident
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("impact")]
        public string Impact { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [JsonPropertyName("resolved_at")]
        public DateTime? ResolvedAt { get; set; }
        [JsonPropertyName("shortlink")]
        public string Shortlink { get; set; }
        [JsonPropertyName("components")]
        public List<AffectedComponent> AffectedComponents { get; set; } = new List<AffectedComponent>();
        [JsonPropertyName("incident_updates")]
        public List<IncidentUpdate> IncidentUpdates { get; set; } = new List<IncidentUpdate>();
    }

    public class AffectedComponent
    {
        public string Name { get; set; }
    }

    public class IncidentUpdate
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
        public string Body { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}