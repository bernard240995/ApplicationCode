using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;

namespace AdvancedCyberSecurityMonitor
{
    public class Program
    {
        private static SecurityMonitor _securityMonitor;
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _cts.Cancel();
            };

            try
            {
                // Initialize security monitor
                _securityMonitor = new SecurityMonitor();
                _securityMonitor.Start();

                Console.WriteLine("Security monitoring started. Press Ctrl+C to exit.");
                Console.WriteLine($"Logs are being written to: {_securityMonitor.LogFilePath}");

                // Keep the application running until cancelled
                while (!_cts.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                _securityMonitor?.Stop();
                Console.WriteLine("Security monitoring stopped.");
            }
        }
    }

    public enum AlertLevel
    {
        Info,
        Warning,
        Critical
    }

    public class SecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public AlertLevel Level { get; set; }
        public required string Source { get; set; }
        public required string Message { get; set; }
        public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Source} - {Message}");
            foreach (var detail in Details)
            {
                sb.AppendLine($"\t{detail.Key}: {detail.Value}");
            }
            return sb.ToString();
        }
    }

    public class ThreatIntelligenceFeed : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _feedUrl;
        private readonly HashSet<IPAddress> _suspiciousIPs = new HashSet<IPAddress>();
        private readonly System.Timers.Timer _refreshTimer;
        private readonly SecurityMonitor _securityMonitor;

        public ThreatIntelligenceFeed(string feedUrl, TimeSpan refreshInterval, SecurityMonitor securityMonitor)
        {
            _securityMonitor = securityMonitor ?? throw new ArgumentNullException(nameof(securityMonitor));
            _feedUrl = feedUrl ?? throw new ArgumentNullException(nameof(feedUrl));

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            // Initialize with some well-known bad IPs
            _suspiciousIPs.UnionWith(new[]
            {
            IPAddress.Parse("45.155.205.233"),  // Known attacker
            IPAddress.Parse("185.220.101.134"), // Tor exit node
            IPAddress.Parse("91.219.236.222")   // Known malware C2
        });

            // Set up periodic refresh - CORRECTED TIMER INITIALIZATION
            _refreshTimer = new System.Timers.Timer(refreshInterval.TotalMilliseconds);
            _refreshTimer.Elapsed += async (sender, e) => await RefreshFeedAsync();
            _refreshTimer.AutoReset = true;
            _refreshTimer.Start();
        }

        public bool IsSuspiciousIP(IPAddress ip)
        {
            if (ip == null)
                throw new ArgumentNullException(nameof(ip));

            return _suspiciousIPs.Contains(ip);
        }

        public IReadOnlyCollection<IPAddress> SuspiciousIPs => _suspiciousIPs.ToList().AsReadOnly();

        private async Task RefreshFeedAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_feedUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var ipList = JsonSerializer.Deserialize<List<string>>(content) ?? new List<string>();

                var newIPs = new HashSet<IPAddress>();
                foreach (var ipString in ipList)
                {
                    if (IPAddress.TryParse(ipString, out var ipAddress))
                    {
                        newIPs.Add(ipAddress);
                    }
                }

                lock (_suspiciousIPs)
                {
                    _suspiciousIPs.Clear();
                    _suspiciousIPs.UnionWith(newIPs);
                }

                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Info,
                    Source = "ThreatIntelligenceFeed",
                    Message = "Threat feed updated successfully",
                    Details = { { "IPCount", _suspiciousIPs.Count.ToString() } }
                });
            }
            catch (Exception ex)
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Warning,
                    Source = "ThreatIntelligenceFeed",
                    Message = "Error updating threat feed",
                    Details = { { "Error", ex.Message } }
                });
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }

    public class SecurityMonitor : IDisposable
    {
        private readonly string _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberSecurityMonitor");
        private readonly string _logFileName = $"SecurityLog_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        private readonly ConcurrentQueue<SecurityEvent> _eventQueue = new ConcurrentQueue<SecurityEvent>();
        private readonly object _logFileLock = new object();
        private readonly List<IDisposable> _monitors = new List<IDisposable>();
        private System.Timers.Timer _logFlushTimer;
        private bool _isRunning;

        public string LogFilePath => Path.Combine(_logDirectory, _logFileName);

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            // Ensure log directory exists
            Directory.CreateDirectory(_logDirectory);

            // Start log flusher
            _logFlushTimer = new System.Timers.Timer(1000);
            _logFlushTimer.Elapsed += FlushLogs;
            _logFlushTimer.Start();

            // Initialize monitoring components
            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.Now,
                Level = AlertLevel.Info,
                Source = "SecurityMonitor",
                Message = "Starting security monitoring system",
                Details = { { "Version", "1.0" }, { "OS", Environment.OSVersion.ToString() } }
            });

            // Check admin privileges
            if (!IsAdministrator())
            {
                LogEvent(new SecurityEvent
                {
                    Timestamp = DateTime.Now,
                    Level = AlertLevel.Warning,
                    Source = "SecurityMonitor",
                    Message = "Application is not running with administrator privileges",
                    Details = { { "Impact", "Some security features may not function properly" } }
                });
            }

            // Start monitoring components
            _monitors.Add(new NetworkMonitor(this));
            _monitors.Add(new ProcessMonitor(this));
            _monitors.Add(new FileIntegrityMonitor(this));
            _monitors.Add(new LoginAttemptMonitor(this));
            _monitors.Add(new SystemEventMonitor(this));

            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.Now,
                Level = AlertLevel.Info,
                Source = "SecurityMonitor",
                Message = "All monitoring components initialized"
            });
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            // Stop all monitors
            foreach (var monitor in _monitors)
            {
                monitor.Dispose();
            }
            _monitors.Clear();

            // Stop log flusher
            _logFlushTimer?.Stop();
            _logFlushTimer?.Dispose();

            // Flush remaining logs
            FlushLogs(null, null);

            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.Now,
                Level = AlertLevel.Info,
                Source = "SecurityMonitor",
                Message = "Security monitoring stopped"
            });
        }

        public void LogEvent(SecurityEvent securityEvent)
        {
            if (securityEvent.Timestamp == default)
                securityEvent.Timestamp = DateTime.Now;

            _eventQueue.Enqueue(securityEvent);

            // Also show critical alerts in console
            if (securityEvent.Level >= AlertLevel.Warning)
            {
                Console.WriteLine($"[{securityEvent.Level}] {securityEvent.Source}: {securityEvent.Message}");
            }
        }

        private void FlushLogs(object sender, ElapsedEventArgs e)
        {
            try
            {
                lock (_logFileLock)
                {
                    using (var writer = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                    {
                        while (_eventQueue.TryDequeue(out var logEntry))
                        {
                            writer.WriteLine(logEntry.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        private bool IsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    public class NetworkMonitor : IDisposable
    {
        private readonly SecurityMonitor _securityMonitor;
        private readonly System.Timers.Timer _scanTimer;
        private readonly HashSet<string> _knownConnections = new HashSet<string>();
        private readonly ThreatIntelligenceFeed _threatFeed;

        public NetworkMonitor(SecurityMonitor securityMonitor)
        {
            _securityMonitor = securityMonitor;

            // Initialize threat feed (refresh every 6 hours)
            _threatFeed = new ThreatIntelligenceFeed(
                "https://threatintel.example.com/api/v1/ips",
                TimeSpan.FromHours(6),
                _securityMonitor);

            // Scan every 30 seconds
            _scanTimer = new System.Timers.Timer(30000);
            _scanTimer.Elapsed += ScanNetwork;
            _scanTimer.Start();

            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Info,
                Source = "NetworkMonitor",
                Message = "Network monitoring initialized",
                Details = {
                    { "ScanInterval", "30 seconds" },
                    { "InitialThreatIPs", _threatFeed.SuspiciousIPs.Count.ToString() }
                }
            });
        }

        private void ScanNetwork(object sender, ElapsedEventArgs e)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnections = properties.GetActiveTcpConnections();
                var udpListeners = properties.GetActiveUdpListeners();

                foreach (var connection in tcpConnections)
                {
                    var connectionKey = $"{connection.LocalEndPoint}-{connection.RemoteEndPoint}-{connection.State}";

                    if (!_knownConnections.Contains(connectionKey))
                    {
                        _knownConnections.Add(connectionKey);

                        var eventDetails = new Dictionary<string, string>
                        {
                            { "LocalEndpoint", connection.LocalEndPoint.ToString() },
                            { "RemoteEndpoint", connection.RemoteEndPoint.ToString() },
                            { "State", connection.State.ToString() },
                            { "Protocol", "TCP" }
                        };

                        // Check threat feed for malicious IPs
                        if (_threatFeed.IsSuspiciousIP(connection.RemoteEndPoint.Address))
                        {
                            _securityMonitor.LogEvent(new SecurityEvent
                            {
                                Level = AlertLevel.Critical,
                                Source = "NetworkMonitor",
                                Message = "Connection to known malicious IP detected",
                                Details = eventDetails
                            });
                        }

                        if (IsSuspiciousPort(connection.LocalEndPoint.Port) ||
                            IsSuspiciousPort(connection.RemoteEndPoint.Port))
                        {
                            _securityMonitor.LogEvent(new SecurityEvent
                            {
                                Level = AlertLevel.Critical,
                                Source = "NetworkMonitor",
                                Message = "Suspicious port detected in network connection",
                                Details = eventDetails
                            });
                        }
                    }
                }

                foreach (var listener in udpListeners)
                {
                    if (IsSuspiciousPort(listener.Port))
                    {
                        _securityMonitor.LogEvent(new SecurityEvent
                        {
                            Level = AlertLevel.Warning,
                            Source = "NetworkMonitor",
                            Message = "Suspicious UDP port listening detected",
                            Details = new Dictionary<string, string>
                            {
                                { "Port", listener.Port.ToString() },
                                { "Protocol", "UDP" }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Warning,
                    Source = "NetworkMonitor",
                    Message = "Error during network scan",
                    Details = { { "Error", ex.Message } }
                });
            }
        }

        private bool IsSuspiciousPort(int port)
        {
            int[] suspiciousPorts = { 4444, 31337, 6667, 5555, 12345, 1337, 8080, 1433, 3306 };
            return suspiciousPorts.Contains(port);
        }

        public void Dispose()
        {
            _scanTimer?.Dispose();
            _threatFeed?.Dispose();
        }
    }

    public class ProcessMonitor : IDisposable
    {
        private readonly SecurityMonitor _securityMonitor;
        private readonly System.Timers.Timer _processCheckTimer;
        private readonly HashSet<int> _knownProcesses = new HashSet<int>();
        private readonly HashSet<string> _suspiciousProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mimikatz", "netcat", "nc", "powersploit", "metasploit", "cain", "john", "hashcat", "wce"
        };

        public ProcessMonitor(SecurityMonitor securityMonitor)
        {
            _securityMonitor = securityMonitor;

            // Scan every 60 seconds
            _processCheckTimer = new System.Timers.Timer(60000);
            _processCheckTimer.Elapsed += CheckProcesses;
            _processCheckTimer.Start();

            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Info,
                Source = "ProcessMonitor",
                Message = "Process monitoring initialized",
                Details = { { "ScanInterval", "60 seconds" } }
            });
        }

        private void CheckProcesses(object sender, ElapsedEventArgs e)
        {
            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        // Detect new processes
                        if (!_knownProcesses.Contains(process.Id))
                        {
                            _knownProcesses.Add(process.Id);

                            var processDetails = new Dictionary<string, string>
                            {
                                { "ProcessID", process.Id.ToString() },
                                { "ProcessName", process.ProcessName },
                                { "StartTime", process.StartTime.ToString("yyyy-MM-dd HH:mm:ss") },
                                { "MemoryUsage", $"{process.WorkingSet64 / 1024 / 1024} MB" }
                            };

                            // Check for suspicious process names
                            if (_suspiciousProcessNames.Contains(process.ProcessName))
                            {
                                _securityMonitor.LogEvent(new SecurityEvent
                                {
                                    Level = AlertLevel.Critical,
                                    Source = "ProcessMonitor",
                                    Message = "Suspicious process detected",
                                    Details = processDetails
                                });
                            }

                            // Check for processes with high privilege
                            if (IsHighPrivilegeProcess(process))
                            {
                                processDetails.Add("Privilege", "High");
                                _securityMonitor.LogEvent(new SecurityEvent
                                {
                                    Level = AlertLevel.Warning,
                                    Source = "ProcessMonitor",
                                    Message = "High privilege process detected",
                                    Details = processDetails
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _securityMonitor.LogEvent(new SecurityEvent
                        {
                            Level = AlertLevel.Warning,
                            Source = "ProcessMonitor",
                            Message = $"Error examining process {process.ProcessName}",
                            Details = { { "Error", ex.Message } }
                        });
                    }
                }

                // Clean up known processes list
                var currentProcessIds = processes.Select(p => p.Id).ToHashSet();
                _knownProcesses.RemoveWhere(id => !currentProcessIds.Contains(id));
            }
            catch (Exception ex)
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Warning,
                    Source = "ProcessMonitor",
                    Message = "Error during process scan",
                    Details = { { "Error", ex.Message } }
                });
            }
        }

        private bool IsHighPrivilegeProcess(Process process)
        {
            try
            {
                var processName = process.ProcessName.ToLower();
                return processName.Contains("admin") ||
                       processName.Contains("root") ||
                       processName.Contains("system") ||
                       processName.Contains("trustedinstaller");
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _processCheckTimer?.Dispose();
        }
    }

    public class FileIntegrityMonitor : IDisposable
    {
        private readonly SecurityMonitor _securityMonitor;
        private readonly FileSystemWatcher _fileWatcher;
        private readonly Dictionary<string, DateTime> _fileLastModified = new Dictionary<string, DateTime>();
        private readonly string[] _criticalFiles =
        {
            Path.Combine(Environment.SystemDirectory, "kernel32.dll"),
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Path.Combine(Environment.SystemDirectory, "explorer.exe"),
            Path.Combine(Environment.SystemDirectory, "lsass.exe")
        };

        public FileIntegrityMonitor(SecurityMonitor securityMonitor)
        {
            _securityMonitor = securityMonitor;

            // Initialize file monitoring
            foreach (var file in _criticalFiles)
            {
                if (File.Exists(file))
                {
                    _fileLastModified[file] = File.GetLastWriteTime(file);
                }
            }

            // Watch for changes in system directory
            _fileWatcher = new FileSystemWatcher(Environment.SystemDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;

            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Info,
                Source = "FileIntegrityMonitor",
                Message = "File integrity monitoring initialized",
                Details = { { "MonitoredDirectory", Environment.SystemDirectory } }
            });
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (_criticalFiles.Contains(e.FullPath))
                {
                    var lastModified = File.GetLastWriteTime(e.FullPath);
                    if (_fileLastModified.TryGetValue(e.FullPath, out var previousModified) &&
                        lastModified != previousModified)
                    {
                        _securityMonitor.LogEvent(new SecurityEvent
                        {
                            Level = AlertLevel.Critical,
                            Source = "FileIntegrityMonitor",
                            Message = "Critical system file modified",
                            Details = new Dictionary<string, string>
                            {
                                { "FilePath", e.FullPath },
                                { "PreviousModified", previousModified.ToString("yyyy-MM-dd HH:mm:ss") },
                                { "NewModified", lastModified.ToString("yyyy-MM-dd HH:mm:ss") }
                            }
                        });

                        _fileLastModified[e.FullPath] = lastModified;
                    }
                }
            }
            catch (Exception ex)
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Warning,
                    Source = "FileIntegrityMonitor",
                    Message = $"Error processing file change event for {e.FullPath}",
                    Details = { { "Error", ex.Message } }
                });
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Warning,
                Source = "FileIntegrityMonitor",
                Message = "New file created in system directory",
                Details = { { "FilePath", e.FullPath } }
            });
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (_criticalFiles.Contains(e.FullPath))
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Critical,
                    Source = "FileIntegrityMonitor",
                    Message = "Critical system file deleted",
                    Details = { { "FilePath", e.FullPath } }
                });
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Warning,
                Source = "FileIntegrityMonitor",
                Message = "File renamed in system directory",
                Details = { { "OldPath", e.OldFullPath }, { "NewPath", e.FullPath } }
            });
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
        }
    }

    public class LoginAttemptMonitor : IDisposable
    {
        private readonly SecurityMonitor _securityMonitor;
        private readonly System.Timers.Timer _loginCheckTimer;
        private readonly Dictionary<string, int> _failedAttempts = new Dictionary<string, int>();

        public LoginAttemptMonitor(SecurityMonitor securityMonitor)
        {
            _securityMonitor = securityMonitor;

            // Check every 5 minutes
            _loginCheckTimer = new System.Timers.Timer(300000);
            _loginCheckTimer.Elapsed += CheckLoginAttempts;
            _loginCheckTimer.Start();

            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Info,
                Source = "LoginAttemptMonitor",
                Message = "Login attempt monitoring initialized",
                Details = { { "ScanInterval", "5 minutes" } }
            });
        }

        private void CheckLoginAttempts(object sender, ElapsedEventArgs e)
        {
            try
            {
                // In a real implementation, this would query security event logs
                // For demonstration, we'll simulate detection of failed attempts
                var random = new Random();
                if (random.Next(0, 100) < 20) // 20% chance to simulate a failed login
                {
                    var username = $"USER{random.Next(1, 5)}";
                    if (!_failedAttempts.ContainsKey(username))
                    {
                        _failedAttempts[username] = 0;
                    }

                    _failedAttempts[username]++;

                    _securityMonitor.LogEvent(new SecurityEvent
                    {
                        Level = AlertLevel.Warning,
                        Source = "LoginAttemptMonitor",
                        Message = "Failed login attempt detected",
                        Details = new Dictionary<string, string>
                        {
                            { "Username", username },
                            { "AttemptCount", _failedAttempts[username].ToString() },
                            { "SourceIP", $"192.168.1.{random.Next(1, 255)}" }
                        }
                    });

                    // Check for brute force attempts
                    if (_failedAttempts[username] >= 5)
                    {
                        _securityMonitor.LogEvent(new SecurityEvent
                        {
                            Level = AlertLevel.Critical,
                            Source = "LoginAttemptMonitor",
                            Message = "Possible brute force attack detected",
                            Details = new Dictionary<string, string>
                            {
                                { "Username", username },
                                { "AttemptCount", _failedAttempts[username].ToString() }
                            }
                        });
                    }
                }

                // Reset counters periodically
                if (random.Next(0, 100) < 30) // 30% chance to reset counters
                {
                    _failedAttempts.Clear();
                }
            }
            catch (Exception ex)
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Warning,
                    Source = "LoginAttemptMonitor",
                    Message = "Error checking login attempts",
                    Details = { { "Error", ex.Message } }
                });
            }
        }

        public void Dispose()
        {
            _loginCheckTimer?.Dispose();
        }
    }

    public class SystemEventMonitor : IDisposable
    {
        private readonly SecurityMonitor _securityMonitor;
        private readonly System.Timers.Timer _eventLogTimer;

        public SystemEventMonitor(SecurityMonitor securityMonitor)
        {
            _securityMonitor = securityMonitor;

            // Check every 10 minutes
            _eventLogTimer = new System.Timers.Timer(600000);
            _eventLogTimer.Elapsed += CheckSystemEvents;
            _eventLogTimer.Start();

            _securityMonitor.LogEvent(new SecurityEvent
            {
                Level = AlertLevel.Info,
                Source = "SystemEventMonitor",
                Message = "System event monitoring initialized",
                Details = { { "ScanInterval", "10 minutes" } }
            });
        }

        private void CheckSystemEvents(object sender, ElapsedEventArgs e)
        {
            try
            {
                // In a real implementation, this would query Windows Event Log
                // For demonstration, we'll simulate some events
                var random = new Random();
                var events = new[]
                {
                    new { Level = AlertLevel.Info, Message = "System time synchronized", Source = "TimeService" },
                    new { Level = AlertLevel.Warning, Message = "Disk space running low on C:", Source = "DiskMonitor" },
                    new { Level = AlertLevel.Critical, Message = "Unexpected system shutdown detected", Source = "System" }
                };

                var selectedEvent = events[random.Next(events.Length)];

                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = selectedEvent.Level,
                    Source = selectedEvent.Source,
                    Message = selectedEvent.Message,
                    Details = { { "EventTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") } }
                });
            }
            catch (Exception ex)
            {
                _securityMonitor.LogEvent(new SecurityEvent
                {
                    Level = AlertLevel.Warning,
                    Source = "SystemEventMonitor",
                    Message = "Error checking system events",
                    Details = { { "Error", ex.Message } }
                });
            }
        }

        public void Dispose()
        {
            _eventLogTimer?.Dispose();
        }
    }
}