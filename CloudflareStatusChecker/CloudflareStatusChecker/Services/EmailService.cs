using CloudflareStatusChecker.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CloudflareStatusChecker.Services
{
    public sealed class EmailService
    {
        public async Task SendEmailWithAttachmentAsync(
            MemoryStream attachmentStream,
            Settings settings,
            string subject,
            CancellationToken cancellationToken = default)
        {
            if (attachmentStream == null) throw new ArgumentNullException(nameof(attachmentStream));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentNullException(nameof(subject));

            if (string.IsNullOrWhiteSpace(settings.EmailFrom))
                throw new ArgumentException("EmailFrom setting is missing");
            if (string.IsNullOrWhiteSpace(settings.EmailTo))
                throw new ArgumentException("EmailTo setting is missing");

            if (string.IsNullOrWhiteSpace(settings.Smtp?.Server))
                throw new ArgumentException("SMTP server configuration is missing");
            if (settings.Smtp.Port <= 0)
                throw new ArgumentException("Invalid SMTP port configuration");
            if (string.IsNullOrWhiteSpace(settings.Smtp.Username))
                throw new ArgumentException("SMTP username is missing");
            if (string.IsNullOrWhiteSpace(settings.Smtp.Password))
                throw new ArgumentException("SMTP password is missing");

            try
            {
                ConsoleHelper.WriteColored("[1/7] Preparing email message...", ConsoleColor.Yellow);

                var message = new MimeMessage
                {
                    Subject = subject
                };
                message.From.Add(MailboxAddress.Parse(settings.EmailFrom));
                message.To.Add(MailboxAddress.Parse(settings.EmailTo));

                if (settings.EmailCC != null && settings.EmailCC.Length > 0)
                {
                    foreach (var cc in settings.EmailCC)
                    {
                        if (!string.IsNullOrWhiteSpace(cc))
                        {
                            message.Cc.Add(MailboxAddress.Parse(cc));
                        }
                    }
                }

                var textBody = new TextPart("plain")
                {
                    Text = "Please find attached the Cloudflare incident report."
                };

                ConsoleHelper.WriteColored("[2/7] Preparing attachment...", ConsoleColor.Yellow);
                attachmentStream.Position = 0;
                var attachment = new MimePart("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    Content = new MimeContent(attachmentStream),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = $"Cloudflare_Incidents_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx"
                };

                message.Body = new Multipart("mixed") { textBody, attachment };

                ConsoleHelper.WriteColored("[3/7] Configuring SMTP client...", ConsoleColor.Yellow);
                using var client = new SmtpClient();

                client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                ConsoleHelper.WriteColored($"[4/7] Connecting to {settings.Smtp.Server}:{settings.Smtp.Port}...", ConsoleColor.Yellow);
                await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, cancellationToken);

                ConsoleHelper.WriteColored("[5/7] Authenticating...", ConsoleColor.Yellow);
                await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password, cancellationToken);

                ConsoleHelper.WriteColored("[6/7] Sending e-mail...", ConsoleColor.Yellow);
                await client.SendAsync(message, cancellationToken);

                ConsoleHelper.WriteColored("[7/7] Disconnecting...", ConsoleColor.Yellow);
                await client.DisconnectAsync(true, cancellationToken);

                ConsoleHelper.WriteColored("E-mail sent successfully!", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteColored($"Error sending email: {ex.Message}", ConsoleColor.Red);
                if (ex.InnerException != null)
                {
                    ConsoleHelper.WriteColored($"Inner error: {ex.InnerException.Message}", ConsoleColor.DarkRed);
                }
                throw;
            }
        }

        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return "*****@*****.***";

            var parts = email.Split('@');
            var user = parts[0];
            var domain = parts[1];

            string Mask(string s) => s.Length switch
            {
                0 => string.Empty,
                1 => "*",
                2 => s[0] + "*",
                _ => s[0] + new string('*', s.Length - 2) + s[^1]
            };

            return $"{Mask(user)}@{Mask(domain)}";
        }
    }
}