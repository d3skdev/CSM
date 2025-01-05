using System.Diagnostics;
using System.Net;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsWPF;
using static NetworkMonitor;


public class UdpConnectionInfo
{
    public IPAddress LocalAddress { get; set; }
    public int LocalPort { get; set; }
    public IPAddress RemoteAddress { get; set; }
    public int RemotePort { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; }
    public DateTime LastSeen { get; set; }
    public string Country { get; set; }
    public string CountryIso { get; set; }
    public bool IsHidden { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public string FormattedTotalBytes => GetFormattedTotalBytes();
    public string ElapsedTime => GetElapsedTimeDisplay();

    private string GetFormattedTotalBytes()
    {
        double totalBytes = BytesSent + BytesReceived;
        if (totalBytes >= 1024 * 1024)
            return $"{(totalBytes / (1024.0 * 1024.0)):F2} MB";
        return $"{(totalBytes / 1024.0):F2} KB";
    }

    private string GetElapsedTimeDisplay()
    {
        var elapsed = DateTime.Now - LastSeen;
        if (elapsed.TotalSeconds < 60)
            return $"{elapsed.TotalSeconds:F0}s ago";
        if (elapsed.TotalMinutes < 60)
            return $"{elapsed.TotalMinutes:F0}m ago";
        return $"{elapsed.TotalHours:F0}h ago";
    }
}

public class NetworkMonitor
{
    private string processId;
    private bool isCapturing = false;
    private TraceEventSession session;
    private Dictionary<string, UdpConnectionInfo> activeConnections = new Dictionary<string, UdpConnectionInfo>();
    public event EventHandler<UdpConnectionInfo[]> ConnectionUpdateEvent;
    private DispatcherTimer timer;


    public NetworkMonitor(string processId)
    {
        this.processId = processId;
        //this.FormClosing += NetworkMonitor_FormClosing;

        // Start auto-refresh timer
        timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(5);
        timer.Tick += (s, e) => UpdateConnectionsList();

    }

    public async void StartMonitoring()
    {
        if (string.IsNullOrEmpty(this.processId)) return;

        if (!isCapturing)
        {
            isCapturing = true;

            lock (activeConnections)
            {
                activeConnections.Clear();
            }

            timer.Start();

            await Task.Run(() =>
        {
            try
            {
                using (session = new TraceEventSession("UdpMonitorSession"))
                {
                    session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                    var parser = session.Source.Kernel;
                    parser.UdpIpSend += Parser_UdpIpEvent;
                    parser.UdpIpRecv += Parser_UdpIpEvent;

                    session.Source.Process();
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error starting ETW session: {ex.Message}",
                //    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        }
        else
        {
            StopMonitoring();
        }
    }
    public void StopMonitoring()
    {
        timer.Stop();

        if (session != null)
        {
            session.Dispose();
            session = null;
        }

        lock (activeConnections)
        {
            activeConnections.Clear();
        }

        isCapturing = false;
    }

    private void Parser_UdpIpEvent(Microsoft.Diagnostics.Tracing.Parsers.Kernel.UdpIpTraceData data)
    {
        try
        {
            var localEndPoint = new IPEndPoint(data.saddr, data.sport);
            var remoteEndPoint = new IPEndPoint(data.daddr, data.dport);
            var key = $"{localEndPoint}-{remoteEndPoint}";
            System.Diagnostics.Debug.WriteLine($"@@@@@@@@{remoteEndPoint}");
            string processName = "Unknown";
            try
            {
                var process = Process.GetProcessById(data.ProcessID);
                processName = process.ProcessName;
            }
            catch { }

            var connInfo = new UdpConnectionInfo
            {
                LocalAddress = localEndPoint.Address,
                LocalPort = localEndPoint.Port,
                RemoteAddress = remoteEndPoint.Address,
                RemotePort = remoteEndPoint.Port,
                ProcessId = data.ProcessID,
                ProcessName = processName,
                LastSeen = DateTime.Now,
                Country = GeoLiteDatabase.GetCountryName(remoteEndPoint.Address.ToString()),
                CountryIso = GeoLiteDatabase.GetCountryName(remoteEndPoint.Address.ToString(), true),
                BytesSent = data.sport == localEndPoint.Port ? data.size : 0,
                BytesReceived = data.dport == localEndPoint.Port ? data.size : 0,
            };

            lock (activeConnections)
            {
                if (activeConnections.ContainsKey(key))
                {
                    var existingConnInfo = activeConnections[key];
                    existingConnInfo.BytesSent += connInfo.BytesSent;
                    existingConnInfo.BytesReceived += connInfo.BytesReceived;
                    existingConnInfo.LastSeen = connInfo.LastSeen;
                }
                else
                {
                    activeConnections[key] = connInfo;
                }
            }
        }
        catch { }
    }



    private void RemoveInactiveConnections()
    {
        var cutoffTime = DateTime.Now.AddSeconds(-10);
        List<string> keysToRemove;

        lock (activeConnections)
        {
            keysToRemove = activeConnections
                .Where(kvp => kvp.Value.LastSeen < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                //activeConnections.Remove(key);
                activeConnections[key].IsHidden = true;
            }
        }
    }

    private void UpdateConnectionsList()
    {
        this.RemoveInactiveConnections();
        UdpConnectionInfo[] filteredConnections;

        lock (activeConnections)
        {
            filteredConnections = activeConnections.Values
                .Where(c => c.LocalPort == 3074)
                .OrderByDescending(c => c.LastSeen)
                .ToArray();
        }

        ConnectionUpdateEvent?.Invoke(this, filteredConnections);
    }
}
