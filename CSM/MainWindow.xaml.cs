using Microsoft.Diagnostics.Tracing.Session;
using SharpPcap;
using System.ComponentModel;
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
            codProcess = new CodProcess();
            codProcess.ProcessExistEvent += onCodProcessExist;
            codProcess.Start();
        }

        private PCAP? pcap;
        private CodProcess codProcess;
        private int codProcessID;

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isOverlayActive;
        public bool IsOverlayActive { get { return _isOverlayActive; } set { _isOverlayActive = value; OnPropertyChanged(); } }



        private string _country;
        public string Country { get { return _country; } set { _country = value; OnPropertyChanged(); } }

        private string _countryIso;
        public string CountryIso { get { return _countryIso; } set { _countryIso = value; OnPropertyChanged(); } }


        private string _city;
        public string City { get { return _city; } set { _city = value; OnPropertyChanged(); } }

        private string _asn;
        public string ASN { get { return _asn; } set { _asn = value; OnPropertyChanged(); } }

        private string _traffic;
        public string Traffic { get { return _traffic; } set { _traffic = value; OnPropertyChanged(); } }


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


        private void updateItemList(ConnectionOverview[] connectionList)
        {
            if (connectionList.Length > 0)
            {
                grid_content.Visibility = Visibility.Visible;
                grid_no_content.Visibility = Visibility.Collapsed;
                Country = connectionList.Last().Country;
                City = connectionList.Last().City;
                ASN = connectionList.Last().ASN;
                Traffic = connectionList.Last().Traffic;
                CountryIso = connectionList.Last().CountryIso;
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