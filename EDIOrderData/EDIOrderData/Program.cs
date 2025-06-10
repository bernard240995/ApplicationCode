using System;
using System.Configuration;
using System.IO;
using System.Text;

namespace EDIFACTToSQL
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Starting EDI to SQL Processor...");
            Console.WriteLine("--------------------------------");

            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EDIOrderData.dll.config");
                Console.WriteLine($"Looking for config at: {configPath}");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException("Configuration file not found", configPath);
                }

                string ediDirectory = GetConfigValue("EdiDirectory");
                string connectionString = GetConfigValue("ConnectionString");
                string smtpServer = GetConfigValue("SmtpServer");
                int smtpPort = GetIntConfigValue("SmtpPort", 587);
                string smtpUsername = GetConfigValue("SmtpUsername");
                string smtpPassword = GetConfigValue("SmtpPassword");
                string fromEmail = GetConfigValue("FromEmail");
                string toEmail = GetConfigValue("ToEmail");

                Console.WriteLine("\nUsing configuration:");
                Console.WriteLine($"- EdiDirectory: {ediDirectory}");
                Console.WriteLine($"- SmtpServer: {smtpServer}");
                Console.WriteLine($"- SmtpPort: {smtpPort}");

                var processor = new EDIProcessor(
                    ediDirectory,
                    connectionString,
                    smtpServer,
                    smtpPort,
                    smtpUsername,
                    smtpPassword,
                    fromEmail,
                    toEmail);

                processor.ProcessDirectory();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Fatal Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nProcessing complete. Press any key to exit...");
                Console.ResetColor();
                Console.ReadKey();
            }
        }

        private static string GetConfigValue(string key)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(value))
            {
                throw new ConfigurationErrorsException($"Missing required configuration value for key: {key}");
            }
            return DecodeBase64(value);
        }

        private static int GetIntConfigValue(string key, int defaultValue)
        {
            string stringValue = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(stringValue))
            {
                return defaultValue;
            }
            return int.TryParse(DecodeBase64(stringValue), out int result) ? result : defaultValue;
        }

        private static string DecodeBase64(string encodedValue)
        {
            try
            {
                encodedValue = encodedValue.Trim();
                int mod4 = encodedValue.Length % 4;
                if (mod4 > 0)
                {
                    encodedValue += new string('=', 4 - mod4);
                }
                byte[] data = Convert.FromBase64String(encodedValue);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return encodedValue;
            }
        }
    }
}