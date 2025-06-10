using AdvancedNetworkMonitor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace AdvancedNetworkMonitor.Services
{
    public static class NetworkScanner
    {
        public static async Task<List<NetworkDevice>> ScanIPRange(string ipRange)
        {
            var devices = new List<NetworkDevice>();
            var (baseIp, start, end) = ParseIPRange(ipRange);

            LoggerService.Log($"Scanning {baseIp}{start} to {baseIp}{end}...", LoggerService.LogLevel.INFO);

            var tasks = new List<Task>();
            for (int i = start; i <= end; i++)
            {
                string ip = baseIp + i;
                tasks.Add(Task.Run(async () =>
                {
                    var device = await CheckDevice(ip);
                    if (device != null)
                    {
                        lock (devices)
                        {
                            devices.Add(device);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return devices;
        }

        private static (string baseIp, int start, int end) ParseIPRange(string ipRange)
        {
            var parts = ipRange.Split('.');
            var baseIp = $"{parts[0]}.{parts[1]}.{parts[2]}.";
            var rangeParts = parts[3].Split('-');
            int start = int.Parse(rangeParts[0]);
            int end = rangeParts.Length > 1 ? int.Parse(rangeParts[1]) : start;
            return (baseIp, Math.Max(1, start), Math.Min(254, end));
        }

        private static async Task<NetworkDevice> CheckDevice(string ipAddress)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    string hostName = "";
                    try
                    {
                        hostName = (await Dns.GetHostEntryAsync(ipAddress)).HostName;
                    }
                    catch { /* Ignore DNS errors */ }

                    return new NetworkDevice
                    {
                        IPAddress = IPAddress.Parse(ipAddress),
                        HostName = hostName,
                        IsReachable = true
                    };
                }
            }
            catch { /* Ignore ping errors */ }
            return null;
        }
    }

    public class NetworkDevice
    {
        public IPAddress IPAddress { get; set; }
        public string HostName { get; set; }
        public bool IsReachable { get; set; }
    }
}