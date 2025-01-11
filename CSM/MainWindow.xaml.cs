using Microsoft.Diagnostics.Tracing.Session;
using SharpPcap;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;


namespace CSM
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            grid_content.Visibility = Visibility.Collapsed;
            codProcess = new CodProcess();
            codProcess.ProcessExistEvent += onCodProcessExist;
            codProcess.Start();
        }

        private PCAP? pcap;
        private CodProcess codProcess;
        private int codProcessID;
        public bool IsOverlayActive = false;

        private ConnectionOverview _connectionOverview;
        public ConnectionOverview connectionOverview { get { return _connectionOverview; } set { _connectionOverview = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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


        private void updateItemList(ConnectionOverview[] connectionsOverviewList)
        {
            if (connectionsOverviewList.Length > 0)
            {
                grid_content.Visibility = Visibility.Visible;
                grid_no_content.Visibility = Visibility.Collapsed;
                connectionOverview = connectionsOverviewList.Last();
            }
            else
            {
                grid_no_content.Visibility = Visibility.Visible;
                grid_content.Visibility = Visibility.Collapsed;
            }
        }




        private void onConnectionUpdate(object? sender, ConnectionOverview[] connectionList)
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
            IsOverlayActive = !IsOverlayActive;

            if (IsOverlayActive)
            {
                this.Height = 100;
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#01FFFFFF"));
                grid_top.Visibility = Visibility.Collapsed;
                grid_bottom.Visibility = Visibility.Collapsed;
                lbl_city.Visibility = Visibility.Collapsed;
                lbl_IP.Visibility = Visibility.Collapsed;
                lbl_asn.Visibility = Visibility.Collapsed;
                cm_always_on_top.IsEnabled = false;
                this.Topmost = true;


            }
            else
            {
                this.Height = 196;
                this.Background = new SolidColorBrush(Colors.Black);
                grid_top.Visibility = Visibility.Visible;
                grid_bottom.Visibility = Visibility.Visible;
                lbl_city.Visibility = Visibility.Visible;
                lbl_IP.Visibility = Visibility.Visible;
                lbl_asn.Visibility = Visibility.Visible;
                cm_always_on_top.IsEnabled = true;
                this.Topmost = false;
            }
        }

        private void cm_exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

    }

}