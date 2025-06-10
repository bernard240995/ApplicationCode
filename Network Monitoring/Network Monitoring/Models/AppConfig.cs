
namespace AdvancedNetworkMonitor.Models
{
    public class AppConfig
    {
        public int ScanIntervalMinutes { get; set; } = 5;       
        public int AlertIntervalMinutes { get; set; } = 1;      
        public string LogFilePath { get; set; } = "network_monitor.log";  
    }
}