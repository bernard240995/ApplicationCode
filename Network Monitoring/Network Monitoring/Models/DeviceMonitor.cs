using System.Net.NetworkInformation;

namespace AdvancedNetworkMonitor.Models
{
    public class DeviceMonitor
    {
        public string IPAddress { get; }
        public string HostName { get; }
        public bool IsOnline { get; private set; }
        public long AverageLatency { get; private set; }

        public DeviceMonitor(string ipAddress, string hostName)
        {
            IPAddress = ipAddress;
            HostName = hostName ?? "Unknown";
        }

        public void UpdateStatus()
        {
            try
            {
                using var ping = new Ping();
                var reply = ping.Send(IPAddress, 1000);
                IsOnline = reply.Status == IPStatus.Success;
                AverageLatency = reply.RoundtripTime;
            }
            catch
            {
                IsOnline = false;
                AverageLatency = 0;
            }
        }
    }
}