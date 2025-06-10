using System;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Linq;
using System.Collections.Generic;
using System.IO;

public class EmailNotifier
{
    private readonly EmailConfig _config;

    public EmailNotifier(EmailConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void SendNotification(bool isError, string subject, string body, List<string> attachments = null)
    {
        try
        {
            using var smtp = new SmtpClient(_config.SmtpServer)
            {
                Port = _config.SmtpPort,
                Credentials = new NetworkCredential(_config.Username, _config.Password),
                EnableSsl = true,
                Timeout = 10000 
            };

            string hostName = System.Net.Dns.GetHostName();
            string ipAddress = GetLocalIPAddress();

            string formattedBody = $@"
                <div style='font-family: Arial, sans-serif;'>
                    <h2 style='color: {(isError ? "#d9534f" : "#5cb85c")};'>
                        {(isError ? "❌ SERVER ALERT" : "✔️ Server Status")}
                    </h2>
                    <h3>System: {hostName}</h3>
                    <h3>IP: {ipAddress}</h3>
                    <h3>Time: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</h3>
                    <hr>
                    {FormatEmailBody(body)}
                    <hr>
                    <p style='font-size: 0.8em; color: #777;'>
                        This is an automated message. Please do not reply directly to this email.
                    </p>
                </div>";

            using var msg = new MailMessage
            {
                From = new MailAddress(_config.FromAddress, "Server Monitoring System"),
                Subject = $"[{hostName}] {(isError ? "ALERT: " : "")}{subject}",
                Body = formattedBody,
                Priority = isError ? MailPriority.High : MailPriority.Normal,
                IsBodyHtml = true
            };

            
            var recipients = isError ? _config.ErrorRecipients : _config.SuccessRecipients;
            foreach (var recipient in recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    msg.To.Add(recipient);
                }
            }

            
            if (_config.CcRecipients != null)
            {
                foreach (var cc in _config.CcRecipients)
                {
                    if (!string.IsNullOrWhiteSpace(cc))
                    {
                        msg.CC.Add(cc);
                    }
                }
            }

            
            if (_config.BccRecipients != null)
            {
                foreach (var bcc in _config.BccRecipients)
                {
                    if (!string.IsNullOrWhiteSpace(bcc))
                    {
                        msg.Bcc.Add(bcc);
                    }
                }
            }

            
            if (attachments != null)
            {
                foreach (var attachmentPath in attachments)
                {
                    if (File.Exists(attachmentPath))
                    {
                        msg.Attachments.Add(new Attachment(attachmentPath));
                    }
                }
            }

            smtp.Send(msg);
            Logger.Log($"Email sent to {string.Join(", ", recipients)}", ConsoleColor.Green);
        }
        catch (SmtpException smtpEx)
        {
            Logger.Log($"[SMTP ERROR] {smtpEx.StatusCode}: {smtpEx.Message}", ConsoleColor.Red);
        }
        catch (Exception ex)
        {
            Logger.Log($"[EMAIL ERROR] {ex.Message}", ConsoleColor.Red);
        }
    }

    public void SendCustomEmail(
        string subject,
        string body,
        IEnumerable<string> toAddresses,
        IEnumerable<string> ccAddresses = null,
        IEnumerable<string> bccAddresses = null,
        IEnumerable<string> attachments = null,
        bool isHtml = true,
        MailPriority priority = MailPriority.Normal)
    {
        try
        {
            using var smtp = new SmtpClient(_config.SmtpServer)
            {
                Port = _config.SmtpPort,
                Credentials = new NetworkCredential(_config.Username, _config.Password),
                EnableSsl = true
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(_config.FromAddress, "Server Monitoring System"),
                Subject = subject,
                Body = isHtml ? FormatEmailBody(body) : body,
                Priority = priority,
                IsBodyHtml = isHtml
            };

            
            foreach (var to in toAddresses ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(to))
                {
                    msg.To.Add(to);
                }
            }

           
            foreach (var cc in ccAddresses ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(cc))
                {
                    msg.CC.Add(cc);
                }
            }

            
            foreach (var bcc in bccAddresses ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(bcc))
                {
                    msg.Bcc.Add(bcc);
                }
            }

            
            foreach (var attachment in attachments ?? Enumerable.Empty<string>())
            {
                if (File.Exists(attachment))
                {
                    msg.Attachments.Add(new Attachment(attachment));
                }
            }

            if (msg.To.Count > 0)
            {
                smtp.Send(msg);
                Logger.Log($"Custom email sent to {string.Join(", ", msg.To)}", ConsoleColor.Green);
            }
            else
            {
                Logger.Log("No valid recipients specified for custom email", ConsoleColor.Yellow);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[CUSTOM EMAIL ERROR] {ex.Message}", ConsoleColor.Red);
        }
    }

    private string FormatEmailBody(string body)
    {
        var sections = body.Split(new[] { "===" }, StringSplitOptions.RemoveEmptyEntries);
        var formattedSections = sections.Select(section =>
        {
            var lines = section.Trim().Split('\n');
            if (lines.Length == 0) return string.Empty;

            string title = lines[0].Trim();
            string content = string.Join("<br/>", lines.Skip(1).Select(l => l.Trim()));

            return $@"
                <div style='border: 1px solid #ddd; border-radius: 5px; padding: 10px; margin-bottom: 15px; background-color: #f9f9f9;'>
                    <h3 style='color: #333; margin-top: 0; border-bottom: 1px solid #eee; padding-bottom: 5px;'>{title}</h3>
                    <div style='padding: 5px;'>{content}</div>
                </div>";
        });

        return string.Join("", formattedSections);
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
                .ToString() ?? "Unknown IP";
        }
        catch
        {
            return "Unknown IP";
        }
    }
}

public class EmailConfig
{
    public required string SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string FromAddress { get; set; }
    public required string[] SuccessRecipients { get; set; }
    public required string[] ErrorRecipients { get; set; }
    public string[] CcRecipients { get; set; }
    public string[] BccRecipients { get; set; }
}