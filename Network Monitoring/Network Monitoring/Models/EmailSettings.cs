namespace AdvancedNetworkMonitor.Models
{
    public class EmailSettings
    {
        // Outlook.com OAuth Configuration
        public string SmtpServer { get; set; } = "smtp.office365.com";
        public int SmtpPort { get; set; } = 587;
        public bool UseSSL { get; set; } = true;

        // OAuth 2.0 Credentials (Register app in Azure AD)
        public string ClientId { get; set; } = "your-client-id";
        public string TenantId { get; set; } = "your-tenant-id";
        public string ClientSecret { get; set; } = "your-client-secret";
        public string FromEmail { get; set; } = "your-email@outlook.com";
        public string ToEmail { get; set; } = "recipient@example.com";

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret) &&
            !string.IsNullOrWhiteSpace(FromEmail) &&
            !string.IsNullOrEmpty(ToEmail);
    }
}