using System;
using System.Net;
using System.Net.Mail;
using EDIOrderData;

namespace EDIFACTToSQL
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _toEmail;

        public EmailService(string smtpServer, int smtpPort, string smtpUsername,
                          string smtpPassword, string fromEmail, string toEmail)
        {
            _smtpServer = smtpServer ?? throw new ArgumentNullException(nameof(smtpServer));
            _smtpPort = smtpPort;
            _smtpUsername = smtpUsername ?? throw new ArgumentNullException(nameof(smtpUsername));
            _smtpPassword = smtpPassword ?? throw new ArgumentNullException(nameof(smtpPassword));
            _fromEmail = fromEmail ?? throw new ArgumentNullException(nameof(fromEmail));
            _toEmail = toEmail ?? throw new ArgumentNullException(nameof(toEmail));
        }

        public void SendOrderNotification(EDIOrder order, string fileName)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            try
            {
                ValidateEmailSettings();

                using var message = new MailMessage(_fromEmail, _toEmail)
                {
                    Subject = $"EDI Order Processed: {order.DocumentNumber}",
                    IsBodyHtml = true,
                    Body = CreateEmailBody(order, fileName)
                };

                using var client = new SmtpClient(_smtpServer, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    EnableSsl = true,
                    Timeout = 10000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                // Add this line to handle Gmail's specific requirements
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                client.Send(message);
                Console.WriteLine($"Notification email sent for order: {order.DocumentNumber}");
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"SMTP Error sending email: {smtpEx.StatusCode} - {smtpEx.Message}");
                if (smtpEx.InnerException != null)
                {
                    Console.WriteLine($"SMTP Inner Exception: {smtpEx.InnerException.Message}");
                }

                // Provide specific guidance for Gmail authentication issues
                if (_smtpServer.Contains("gmail.com"))
                {
                    Console.WriteLine("\nGmail Authentication Help:");
                    Console.WriteLine("1. Ensure 'Less secure app access' is enabled in your Google Account settings");
                    Console.WriteLine("2. If using 2FA, create an App Password and use that instead of your regular password");
                    Console.WriteLine("3. Make sure your account isn't locked for suspicious activity");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email notification: {ex.Message}");
            }
        }

        private void ValidateEmailSettings()
        {
            if (string.IsNullOrWhiteSpace(_smtpServer))
                throw new ArgumentException("SMTP server is not configured");

            if (string.IsNullOrWhiteSpace(_smtpUsername) || string.IsNullOrWhiteSpace(_smtpPassword))
                throw new ArgumentException("SMTP credentials are not configured");

            if (string.IsNullOrWhiteSpace(_fromEmail) || string.IsNullOrWhiteSpace(_toEmail))
                throw new ArgumentException("Email addresses are not configured");

            if (_smtpPort <= 0 || _smtpPort > 65535)
                throw new ArgumentException("Invalid SMTP port number");
        }

        private string CreateEmailBody(EDIOrder order, string fileName)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
        .container {{ max-width: 800px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078D7; color: white; padding: 15px; text-align: center; }}
        .content {{ border: 1px solid #ddd; padding: 20px; margin-top: 20px; }}
        .section {{ margin-bottom: 15px; }}
        .section-title {{ font-weight: bold; color: #0078D7; margin-bottom: 5px; }}
        .border-bottom {{ border-bottom: 1px solid #eee; padding-bottom: 10px; margin-bottom: 10px; }}
        .footer {{ margin-top: 20px; font-size: 0.8em; color: #666; text-align: center; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
        th, td {{ padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>EDI Order Notification</h2>
        </div>
        
        <div class='content'>
            <div class='section border-bottom'>
                <div class='section-title'>File Information</div>
                <div><strong>File Name:</strong> {fileName}</div>
                <div><strong>Processed At:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>
            </div>
            
            <div class='section border-bottom'>
                <div class='section-title'>Order Summary</div>
                <div><strong>Document Number:</strong> {order.DocumentNumber}</div>
                <div><strong>Message Reference:</strong> {order.MessageReference}</div>
                <div><strong>Document Date:</strong> {order.DocumentDate}</div>
                <div><strong>Delivery Date:</strong> {order.DeliveryDate}</div>
            </div>
            
            <div class='section border-bottom'>
                <div class='section-title'>Parties</div>
                <table>
                    <tr>
                        <th>Role</th>
                        <th>ID</th>
                        <th>Name</th>
                    </tr>
                    <tr>
                        <td>Buyer</td>
                        <td>{order.BuyerId}</td>
                        <td>{order.BuyerName}</td>
                    </tr>
                    <tr>
                        <td>Supplier</td>
                        <td>{order.SupplierId}</td>
                        <td>{order.SupplierName}</td>
                    </tr>
                </table>
            </div>
            
            <div class='section'>
                <div class='section-title'>Item Details</div>
                <table>
                    <tr>
                        <th>Item Number</th>
                        <th>Quantity</th>
                        <th>Unit Price</th>
                        <th>Line Amount</th>
                    </tr>
                    <tr>
                        <td>{order.ItemNumber}</td>
                        <td>{order.Quantity}</td>
                        <td>{order.UnitPrice}</td>
                        <td>{order.LineAmount}</td>
                    </tr>
                </table>
            </div>
        </div>
        
        <div class='footer'>
            <p>This is an automated notification. Please do not reply to this email.</p>
            <p>© {DateTime.Now.Year} EDI Processing System</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}