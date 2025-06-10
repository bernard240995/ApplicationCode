using AdvancedNetworkMonitor.Models;
using AdvancedNetworkMonitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AdvancedNetworkMonitor
{
    class Program
    {
        private static List<DeviceMonitor> _monitoredDevices = new List<DeviceMonitor>();
        private static Timer _statusTimer;
        private static Timer _alertTimer;
        private static bool _isRunning = true;
        private static EmailSettings _emailSettings = new EmailSettings();

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔══════════════════════════════════╗
║    NETWORK MONITOR v2.0          ║
║    Scanning your network...      ║
╚══════════════════════════════════╝
");
            Console.ResetColor();

            LoggerService.Log("Initializing Network Monitor...", LoggerService.LogLevel.INFO);

            // Initialize email settings
            if (!_emailSettings.IsValid)
            {
                LoggerService.Log("Email alerts disabled - invalid configuration", LoggerService.LogLevel.WARNING);
            }
            else
            {
                LoggerService.Log("Email alerts enabled", LoggerService.LogLevel.SUCCESS);
            }

            // Scan network
            string baseIp = GetLocalIPAddress();
            string ipRange = baseIp.Substring(0, baseIp.LastIndexOf('.') + 1) + "1-254";
            LoggerService.Log($"Scanning network range: {ipRange}", LoggerService.LogLevel.INFO);

            var devices = await NetworkScanner.ScanIPRange(ipRange);
            foreach (var device in devices)
            {
                _monitoredDevices.Add(new DeviceMonitor(device.IPAddress.ToString(), device.HostName));
                LoggerService.Log($"Found device: {device.IPAddress} ({device.HostName})",
                    device.IsReachable ? LoggerService.LogLevel.SUCCESS : LoggerService.LogLevel.WARNING);
            }

            // Start monitoring (30-second checks, 1-minute alerts)
            _statusTimer = new Timer(CheckDeviceStatus, null, 0, 30000);
            _alertTimer = new Timer(CheckForAlerts, null, 0, 60000);

            LoggerService.Log("Monitoring started. Press Q to quit.", LoggerService.LogLevel.INFO);

            while (_isRunning)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                    _isRunning = false;
                Thread.Sleep(100);
            }

            _statusTimer.Dispose();
            _alertTimer.Dispose();
            LoggerService.Log("Monitoring stopped", LoggerService.LogLevel.INFO);
        }

        private static void CheckDeviceStatus(object state)
        {
            LoggerService.Log("Checking device statuses...", LoggerService.LogLevel.DEBUG);
            Parallel.ForEach(_monitoredDevices, device =>
            {
                var wasOnline = device.IsOnline;
                device.UpdateStatus();

                if (device.IsOnline != wasOnline)
                {
                    var status = device.IsOnline ? "ONLINE" : "OFFLINE";
                    var logLevel = device.IsOnline ? LoggerService.LogLevel.SUCCESS : LoggerService.LogLevel.ERROR;
                    LoggerService.Log($"{device.IPAddress} status changed to {status} (Latency: {device.AverageLatency}ms)", logLevel);
                }
            });
        }

        private static async void CheckForAlerts(object state)
        {
            var offlineDevices = _monitoredDevices.Where(d => !d.IsOnline).ToList();
            if (offlineDevices.Count > 0 && _emailSettings.IsValid)
            {
                LoggerService.Log($"Found {offlineDevices.Count} offline devices", LoggerService.LogLevel.WARNING);
                try
                {
                    await EmailService.SendDeviceAlertEmail(_emailSettings, offlineDevices);
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"Failed to send alert email: {ex.Message}", LoggerService.LogLevel.ERROR);
                }
            }
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Log($"Failed to detect local IP: {ex.Message}", LoggerService.LogLevel.ERROR);
            }
            return "127.0.0.1";
        }
    }
}