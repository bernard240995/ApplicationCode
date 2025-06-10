using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class RamChecker
{
    private const double HighMemoryThreshold = 90.0; 

    public static string Check()
    {
        try
        {
            
            try
            {
                return CheckUsingPerformanceCounters();
            }
            catch
            {
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return CheckUsingWindowsAPI();
                }
                return "[WARNING] RAM check not supported on this platform";
            }
        }
        catch (Exception ex)
        {
            return $"[ERROR] RAM check failed: {ex.Message}";
        }
    }

    private static string CheckUsingPerformanceCounters()
    {
       
        var availableCounter = new PerformanceCounter("Memory", "Available Bytes");
        var committedCounter = new PerformanceCounter("Memory", "Committed Bytes");

        float availableBytes = availableCounter.NextValue();
        float committedBytes = committedCounter.NextValue();

        
        var totalMemoryCounter = new PerformanceCounter("Memory", "Cache Bytes");
        float totalBytes = committedBytes + availableBytes;

        double percentageUsed = (committedBytes / totalBytes) * 100;

        
        double totalGB = totalBytes / (1024 * 1024 * 1024);
        double usedGB = committedBytes / (1024 * 1024 * 1024);

        if (percentageUsed >= HighMemoryThreshold)
        {
            return $"[WARNING] High RAM usage: {percentageUsed:0.00}% ({usedGB:0.00}GB used of {totalGB:0.00}GB total)";
        }

        return string.Empty;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private static string CheckUsingWindowsAPI()
    {
        MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
        memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

        if (!GlobalMemoryStatusEx(ref memStatus))
        {
            return "[ERROR] Failed to get memory status using Windows API";
        }

        double percentageUsed = memStatus.dwMemoryLoad;
        double totalGB = memStatus.ullTotalPhys / (1024.0 * 1024 * 1024);
        double usedGB = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024 * 1024);

        if (percentageUsed >= HighMemoryThreshold)
        {
            return $"[WARNING] High RAM usage: {percentageUsed:0.00}% ({usedGB:0.00}GB used of {totalGB:0.00}GB total)";
        }

        return string.Empty;
    }
}