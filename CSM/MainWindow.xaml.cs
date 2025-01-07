using Microsoft.Diagnostics.Tracing.Session;
using SharpPcap;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


namespace CSM
{
    public partial class MainWindow : Window
    {
        private PCAP? pcap;
        private CodProcess codProcess;
        private int codProcessID;
        private bool isOverlayActive;
        public MainWindow()
        {
            InitializeComponent();
            codProcess = new CodProcess();
            codProcess.ProcessExistEvent += onCodProcessExist;
            codProcess.Start();
        }

        private void onCodProcessExist(object? sender, int processID)
        {
            if (processID == -1)
            {
                lbl_connection_state.Content = "Disconnected";
                lbl_connection_state.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                codProcessID = processID;
                lbl_connection_state.Content = "Connected";
                lbl_connection_state.Foreground = new SolidColorBrush(Colors.LimeGreen);

                if (pcap == null)
                {
                    if (cb_deviceComboBox.SelectedItem != null)
                    {
                        var selectedDevice = ((dynamic)cb_deviceComboBox.SelectedItem).Device as ILiveDevice;
                        if (selectedDevice != null)
                        {
                            pcap = new PCAP(selectedDevice, codProcessID.ToString());
                            pcap.StartMonitoring();
                            pcap.ConnectionUpdateEvent += onConnectionUpdate;
                        }
                    }
                }
            }
        }


        private void updateItemList(ConnectionListView[] connectionList)
        {
            var items = new List<ConnectionListView>();

            foreach (var c in connectionList)
            {
                items.Add(new ConnectionListView
                {
                    Country = $"{c.Country}",
                    Traffic = $"{c.RemoteIp} ({c.Traffic})",
                    CountryIso = c.CountryIso
                });

            }

            CustomListBox.ItemsSource = items;
        }

        private void onConnectionUpdate(object? sender, ConnectionListView[] connectionList)
        {
            this.updateItemList(connectionList);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Check for admin privileges
            bool? isElevated = TraceEventSession.IsElevated();
            if (isElevated.HasValue && !isElevated.Value)
            {
                MessageBox.Show("This application requires administrator privileges to monitor network events.",
                    "Administrator Rights Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }

            // Check if Npcap is installed
            try
            {
                var devices = CaptureDeviceList.Instance;
                if (devices.Count == 0)
                {
                    MessageBox.Show("No capture devices found. Please ensure Npcap is installed.",
                        "Npcap Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Application.Current.Shutdown();
                    return;
                }
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show("Npcap is not installed. Please install Npcap to use this application.\n" +
                    "You can download it from https://npcap.com/",
                    "Npcap Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
                return;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (pcap != null)
            {
                pcap.StopMonitoring();
            }
            codProcess.Stop();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void cb_deviceComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (pcap != null)
            {
                pcap.StopMonitoring();
                pcap = null;
            }

            if (codProcessID != 0 && cb_deviceComboBox.SelectedItem != null)
            {
                var selectedDevice = ((dynamic)cb_deviceComboBox.SelectedItem).Device as ILiveDevice;
                if (selectedDevice != null)
                {
                    pcap = new PCAP(selectedDevice, codProcessID.ToString());
                    pcap.StartMonitoring();
                    pcap.ConnectionUpdateEvent += onConnectionUpdate;
                }
            }
        }

        private void cm_overlay_Click(object sender, RoutedEventArgs e)
        {
            isOverlayActive = !isOverlayActive;

            if (isOverlayActive)
            {
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#01FFFFFF"));
                lbl_header.Visibility = Visibility.Hidden;
                cb_deviceComboBox.Visibility = Visibility.Hidden;
                lbl_connection_state.Visibility = Visibility.Hidden;
                cm_always_on_top.IsEnabled = false;
                this.Topmost = true;


            }
            else
            {
                this.Background = new SolidColorBrush(Colors.Black);
                lbl_header.Visibility = Visibility.Visible;
                cb_deviceComboBox.Visibility = Visibility.Visible;
                lbl_connection_state.Visibility = Visibility.Visible;
                cm_always_on_top.IsEnabled = true;
                this.Topmost = false;
            }

        }


        private void cm_always_on_top_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
        }

        private void cm_exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

    }

}