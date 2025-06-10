using System;

namespace CloudflareStatusChecker.Models
{
    public class Settings
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string UnresolvedApiUrl { get; set; } = string.Empty;
        public string EmailFrom { get; set; } = string.Empty;
        public string EmailTo { get; set; } = string.Empty;
        public string[]? EmailCC { get; set; }
        public bool BypassSslValidation { get; set; } = false;
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
    }

    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;
        public int Timeout { get; set; } = 30000;
    }
}