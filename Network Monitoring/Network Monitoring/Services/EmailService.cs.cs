using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdvancedNetworkMonitor.Models;
using MailKit.Security;

namespace AdvancedNetworkMonitor.Services
{
    public static class EmailService
    {
        public static async Task SendDeviceAlertEmail(EmailSettings settings, List<DeviceMonitor> offlineDevices)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Network Monitor", settings.FromEmail));
            message.To.Add(new MailboxAddress("Admin", settings.ToEmail));
            message.Subject = $"Network Alert: {offlineDevices.Count} Device(s) Offline";

            message.Body = new TextPart("plain")
            {
                Text = $"Offline Devices:\n\n" +
                       string.Join("\n", offlineDevices.Select(d => $"- {d.IPAddress} ({d.HostName})")) +
                       $"\n\nDetection Time: {DateTime.Now}"
            };

            using var client = new SmtpClient();

            try
            {
                // Get OAuth 2.0 Token
                var app = ConfidentialClientApplicationBuilder
                    .Create(settings.ClientId)
                    .WithClientSecret(settings.ClientSecret)
                    .WithTenantId(settings.TenantId)
                    .Build();

                var scopes = new[] { "https://outlook.office365.com/.default" };
                var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();

                // Connect with OAuth
                await client.ConnectAsync(settings.SmtpServer, settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                var oauth2 = new SaslMechanismOAuth2(settings.FromEmail, authResult.AccessToken);
                await client.AuthenticateAsync(oauth2);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                LoggerService.Log("Alert email sent successfully", LoggerService.LogLevel.SUCCESS);
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Failed to send email: {ex.Message}", LoggerService.LogLevel.ERROR);
                throw;
            }
        }
    }
}