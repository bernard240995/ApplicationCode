using System.Net;

namespace AdvancedNetworkMonitor.Models
{
    public class NetworkDevice
    {
        public IPAddress IPAddress { get; set; }
        public string HostName { get; set; }
        public bool IsReachable { get; set; }
        public string MACAddress { get; set; }
        public string Manufacturer { get; set; }

        public override string ToString()
        {
            return $"{IPAddress} ({HostName ?? "Unknown"}) [{(IsReachable ? "Online" : "Offline")}]";
        }
    }
}
