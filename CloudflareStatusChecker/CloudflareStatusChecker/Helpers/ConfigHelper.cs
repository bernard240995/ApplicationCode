using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Xml;
using CloudflareStatusChecker.Models;

namespace CloudflareStatusChecker
{
    public static class ConfigHelper
    {
        public static Settings LoadSettings()
        {
            var settings = new Settings();

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("CloudflareStatusChecker.CloudflareStatusChecker.dll.config");
                using var reader = new StreamReader(stream);

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(reader.ReadToEnd());

                settings.ApiUrl = GetConfigValue(xmlDoc, "ApiUrl");
                settings.UnresolvedApiUrl = GetConfigValue(xmlDoc, "UnresolvedApiUrl");
                settings.EmailFrom = GetConfigValue(xmlDoc, "EmailFrom");
                settings.EmailTo = GetConfigValue(xmlDoc, "EmailTo");
                settings.BypassSslValidation = bool.Parse(GetConfigValue(xmlDoc, "BypassSslValidation") ?? "false");

                settings.Smtp = new SmtpSettings
                {
                    Server = GetConfigValue(xmlDoc, "Smtp.Server") ?? GetConfigValue(xmlDoc, "Smtp:Server"),
                    Port = int.Parse(GetConfigValue(xmlDoc, "Smtp.Port") ?? GetConfigValue(xmlDoc, "Smtp:Port") ?? "587"),
                    Username = DecodeBase64(GetConfigValue(xmlDoc, "Smtp.Username") ?? GetConfigValue(xmlDoc, "Smtp:Username")),
                    Password = DecodeBase64(GetConfigValue(xmlDoc, "Smtp.Password") ?? GetConfigValue(xmlDoc, "Smtp:Password")),
                    UseSsl = bool.Parse(GetConfigValue(xmlDoc, "Smtp.UseSsl") ?? GetConfigValue(xmlDoc, "Smtp:UseSsl") ?? "true"),
                    Timeout = int.Parse(GetConfigValue(xmlDoc, "Smtp.Timeout") ?? GetConfigValue(xmlDoc, "Smtp:Timeout") ?? "30000")
                };

                
                if (settings.Smtp.Server.Contains("gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Smtp.UseSsl = true;
                    settings.Smtp.Port = 587; 
                }

                return settings;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteColored($"Error loading configuration: {ex.Message}", ConsoleColor.Red);
                throw;
            }
        }

        private static string GetConfigValue(XmlDocument xmlDoc, string key)
        {
            var node = xmlDoc.SelectSingleNode($"/configuration/appSettings/add[@key='{key}']");
            return node?.Attributes?["value"]?.Value;
        }

        private static string DecodeBase64(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return string.Empty;
            try
            {
                var bytes = Convert.FromBase64String(encoded);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return encoded;
            }
        }
    }
}