using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CSM
{
    public class ConnectionStats
    {
        public string RemoteIp { get; set; }
        public long TotalBytes { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public class ConnectionListView
    {
        public string RemoteIp { get; set; }
        public string Country { get; set; }
        public string CountryIso { get; set; }
        public string Traffic { get; set; }
        public string LastActivity { get; set; }
    }

    public class PacketCaptureConfig
    {
        public const int UI_REFRESH_INTERVAL = 3000; // 3 seconds
        public const int CONNECTION_TIMEOUT = 3; // 5 seconds
        public const int MIN_BYTES_THRESHOLD = 1024; // 1KB minimum to show connection
        public const string DEFAULT_FILTER = "udp dst port 3074";
    }

    public class ConnectionManager
    {
        private readonly Dictionary<string, ConnectionStats> _stats = new();
        private readonly Dictionary<string, ConnectionListView> _listView = new();
        private readonly Dictionary<string, long> _pendingUpdates = new();
        
        public IReadOnlyDictionary<string, ConnectionListView> ListView => _listView;

        public void UpdateStats(string remoteIp, int dataLength)
        {
            if (!_pendingUpdates.ContainsKey(remoteIp))
            {
                _pendingUpdates[remoteIp] = 0;
            }
            _pendingUpdates[remoteIp] += dataLength;
        }

        public void ApplyPendingUpdates()
        {
            foreach (var (remoteIp, bytes) in _pendingUpdates)
            {
                if (!_stats.TryGetValue(remoteIp, out var stats))
                {
                    stats = new ConnectionStats { RemoteIp = remoteIp, TotalBytes = 0, LastActivity = DateTime.Now };
                    _stats[remoteIp] = stats;
                }

                stats.TotalBytes += bytes;
                stats.LastActivity = DateTime.Now;

                UpdateListViewItem(remoteIp, stats);
            }

            _pendingUpdates.Clear();
        }

        private void UpdateListViewItem(string remoteIp, ConnectionStats stats)
        {
            if (stats.TotalBytes >= PacketCaptureConfig.MIN_BYTES_THRESHOLD)
            {
                if (!_listView.ContainsKey(remoteIp))
                {
                    _listView[remoteIp] = new ConnectionListView
                    {
                        RemoteIp = remoteIp,
                        Traffic = ByteFormatter.Format(stats.TotalBytes),
                        Country = GeoLiteDatabase.GetCountryName(remoteIp),
                        CountryIso = GeoLiteDatabase.GetCountryName(remoteIp, true),
                        LastActivity = DateTime.Now.ToString("HH:mm:ss")
                    };
                }
                else
                {
                    _listView[remoteIp].LastActivity = DateTime.Now.ToString("HH:mm:ss");
                    _listView[remoteIp].Traffic = ByteFormatter.Format(stats.TotalBytes);
                }
            }
            else if (_listView.ContainsKey(remoteIp))
            {
                _listView.Remove(remoteIp);
            }
        }

        public void RemoveInactiveConnections(int timeoutSeconds)
        {
            var inactiveIps = _stats
                .Where(kvp => (DateTime.Now - kvp.Value.LastActivity).TotalSeconds >= timeoutSeconds)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ip in inactiveIps)
            {
                _listView.Remove(ip);
                _stats.Remove(ip);
            }
        }

        public void Clear()
        {
            _stats.Clear();
            _listView.Clear();
            _pendingUpdates.Clear();
        }
    }

    public static class ByteFormatter
    {
        private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "TB" };

        public static string Format(long bytes)
        {
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < Sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {Sizes[order]}";
        }
    }

    public class PacketProcessor
    {
        private readonly ConnectionManager _connectionManager;

        public PacketProcessor(ConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        public void ProcessPacket(PacketCapture e)
        {
            try
            {
                var rawPacket = e.GetPacket();
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);
                var ipPacket = packet.Extract<IPPacket>();
                var udpPacket = packet.Extract<UdpPacket>();

                if (ipPacket == null || udpPacket == null) return;

                string remoteIp = ipPacket.SourceAddress.ToString();
                if (ipPacket.SourceAddress.Equals(IPAddress.Parse("127.0.0.1"))) return;

                _connectionManager.UpdateStats(remoteIp, rawPacket.Data.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing packet: {ex.Message}");
                throw;
            }
        }
    }

    public class PCAP
    {
        private readonly string _processId;
        private readonly ILiveDevice _device;
        private readonly DispatcherTimer _refreshTimer;
        private readonly ConnectionManager _connectionManager;
        private readonly PacketProcessor _packetProcessor;
        private DateTime _lastUiUpdate;

        public event EventHandler<ConnectionListView[]> ConnectionUpdateEvent;

        public PCAP(ILiveDevice selectedDevice, string processId)
        {
            _device = selectedDevice ?? throw new ArgumentNullException(nameof(selectedDevice));
            _processId = processId;
            _connectionManager = new ConnectionManager();
            _packetProcessor = new PacketProcessor(_connectionManager);
            _lastUiUpdate = DateTime.Now;

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PacketCaptureConfig.UI_REFRESH_INTERVAL)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            _connectionManager.RemoveInactiveConnections(PacketCaptureConfig.CONNECTION_TIMEOUT);
            ConnectionUpdateEvent?.Invoke(this, _connectionManager.ListView.Values.ToArray());
        }

        public void StartMonitoring()
        {
            try
            {
                if (_device == null)
                {
                    throw new InvalidOperationException("No network device selected.");
                }

                _connectionManager.Clear();
                InitializeDeviceCapture();
                _refreshTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting monitoring: {ex.Message}");
                StopMonitoring();
                throw;
            }
        }

        public void StopMonitoring()
        {
            try
            {
                if (_device != null)
                {
                    _device.StopCapture();
                    _device.OnPacketArrival -= Device_OnPacketArrival;
                    _device.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping capture: {ex.Message}");
                throw;
            }
            finally
            {
                _refreshTimer.Stop();
            }
        }

        private void InitializeDeviceCapture()
        {
            _device.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                ReadTimeout = 1000
            });
            _device.Filter = PacketCaptureConfig.DEFAULT_FILTER;
            _device.OnPacketArrival += Device_OnPacketArrival;
            _device.StartCapture();
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            _packetProcessor.ProcessPacket(e);

            if ((DateTime.Now - _lastUiUpdate).TotalSeconds >= 1)
            {
                _connectionManager.ApplyPendingUpdates();
                _lastUiUpdate = DateTime.Now;
            }
        }
    }
}
