using SharpPcap;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace CSM
{
    public class NetworkDeviceComboBox : ComboBox
    {
        private ILiveDevice selectedDevice;

        public NetworkDeviceComboBox()
        {
            // Subscribe to the SelectionChanged event
            this.SelectionChanged += NetworkDeviceComboBox_SelectionChanged;
            LoadNetworkDevices();
        }

        private void NetworkDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Raise a custom event or perform actions when the selection changes
            if (this.SelectedItem != null)
            {
                var selected = this.SelectedItem;
                selectedDevice = ((dynamic)selected).Device as ILiveDevice;
            }
            OnSelectionChanged();
        }

        // Custom event or method to handle selection change
        public event SelectionChangedEventHandler CustomSelectionChanged;

        protected virtual void OnSelectionChanged()
        {
            CustomSelectionChanged?.Invoke(this, new SelectionChangedEventArgs(SelectionChangedEvent, new List<object>(), new List<object>()));
        }


        private void LoadNetworkDevices()
        {
            try
            {
                var devices = CaptureDeviceList.Instance;
                if (devices.Count == 0)
                {
                    MessageBox.Show("No capture devices found.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get all network interfaces that are up, have IPv4 addresses and a default gateway
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                                ni.GetIPProperties().UnicastAddresses
                                    .Any(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) &&
                                ni.GetIPProperties().GatewayAddresses
                                    .Any(gateway => gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    .ToList();

                var deviceList = devices.Select(d => new
                {
                    Device = d,
                    Description = $"{d.Description}",
                    NetworkInterface = networkInterfaces.FirstOrDefault(ni =>
                        d.Name.Contains(ni.Id) || ni.Description.Contains(d.Description))
                })
                .Where(d => d.Device != null && d.NetworkInterface != null)
                .ToList();

                this.ItemsSource = deviceList;
                this.DisplayMemberPath = "Description";
                this.SelectedValuePath = "Device";

                // Auto-select the best interface
                var bestDevice = deviceList
                    .OrderByDescending(d => d.NetworkInterface.Speed) // Prefer faster connections
                    .FirstOrDefault();

                if (bestDevice != null)
                {
                    this.SelectedItem = bestDevice;
                }
                else if (this.Items.Count > 0)
                {
                    this.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading network devices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}