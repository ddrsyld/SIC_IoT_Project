using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DashboardMonitorBateraiManajemen.Models;
using DashboardMonitorBateraiManajemen.Services;

namespace DashboardMonitorBateraiManajemen.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SerialPortService _serialService;
        private readonly MqttService _mqttService;
        private readonly DispatcherTimer _simulationTimer;
        private readonly DispatcherTimer _chartRefreshTimer;
        private readonly Random _rand = new();

        // History buffer untuk Waveform Graph (Maksimum 60 titik sampel)
        private const int MaxHistoryPoints = 60;
        private readonly List<double> _voltageHistory = new();
        private readonly List<double> _currentHistory = new();
        private readonly List<double> _tempHistory = new();

        public MainViewModel()
        {
            // Inisialisasi Service Serial
            _serialService = new SerialPortService();
            _serialService.OnRawDataReceived += OnIncomingRawData;
            _serialService.OnTelemetryParsed += OnIncomingTelemetryParsed;
            _serialService.OnConnectionStateChanged += OnServiceConnectionState;
            _serialService.OnError += OnServiceError;

            // Inisialisasi Service MQTT
            _mqttService = new MqttService();
            _mqttService.OnRawDataReceived += OnIncomingRawData;
            _mqttService.OnTelemetryParsed += OnIncomingTelemetryParsed;
            _mqttService.OnConnectionStateChanged += OnServiceConnectionState;
            _mqttService.OnError += OnServiceError;

            // Inisialisasi Port Serial
            RefreshPorts();
            SelectedBaudRate = 115200;

            // Timer Demo Simulator
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _simulationTimer.Tick += SimulationTimer_Tick;

            // Timer update grafik koordinat
            _chartRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _chartRefreshTimer.Tick += (s, e) => UpdateChartPoints();
            _chartRefreshTimer.Start();

            // Seed initial history
            InitializeDummyHistory();
        }

        #region Properties - Sensor Data

        private double _voltage = 48.60;
        public double Voltage
        {
            get => _voltage;
            set
            {
                if (SetField(ref _voltage, value))
                {
                    OnPropertyChanged(nameof(Power));
                    UpdateExtrema();
                }
            }
        }

        private double _current = 14.80;
        public double Current
        {
            get => _current;
            set
            {
                if (SetField(ref _current, value))
                {
                    OnPropertyChanged(nameof(Power));
                    OnPropertyChanged(nameof(OperatingMode));
                    OnPropertyChanged(nameof(IsCharging));
                    UpdateExtrema();
                }
            }
        }

        private double _temperature = 31.4;
        public double Temperature
        {
            get => _temperature;
            set
            {
                if (SetField(ref _temperature, value))
                {
                    OnPropertyChanged(nameof(ThermalStatus));
                    UpdateExtrema();
                }
            }
        }

        private double _soc = 86.0;
        public double SOC
        {
            get => _soc;
            set => SetField(ref _soc, Math.Clamp(value, 0, 100));
        }

        private double _capacityAh = 86.0;
        public double CapacityAh
        {
            get => _capacityAh;
            set
            {
                if (SetField(ref _capacityAh, Math.Clamp(value, 0, 1000)))
                {
                    OnPropertyChanged(nameof(CapacityPercentage));
                    OnPropertyChanged(nameof(CapacityStatus));
                }
            }
        }

        public double NominalCapacityAh => 100.0; // Kapasitas Nominal Pack (100 Ah)
        public double CapacityPercentage => Math.Clamp(Math.Round((CapacityAh / NominalCapacityAh) * 100.0, 1), 0, 100);

        public double Power => Math.Round(Voltage * Current, 2);
        public bool IsCharging => Current > 0.05;
        public string OperatingMode => IsCharging ? "CHARGING (+)" : (Current < -0.05 ? "DISCHARGING (-)" : "IDLE (0.0 A)");
        public string CapacityStatus => CapacityAh >= 80 ? "OPTIMAL (> 80 Ah)" : (CapacityAh >= 40 ? "NORMAL (40-80 Ah)" : "RENDAH (< 40 Ah)");
        public string ThermalStatus => Temperature <= 45 ? "NORMAL" : (Temperature <= 60 ? "WARM" : "HIGH TEMP");

        // Min / Max Extrema Tracker
        private double _minVoltage = 48.0;
        public double MinVoltage { get => _minVoltage; set => SetField(ref _minVoltage, value); }

        private double _maxVoltage = 52.0;
        public double MaxVoltage { get => _maxVoltage; set => SetField(ref _maxVoltage, value); }

        private double _maxCurrent = 25.0;
        public double MaxCurrent { get => _maxCurrent; set => SetField(ref _maxCurrent, value); }

        private double _maxTemperature = 35.0;
        public double MaxTemperature { get => _maxTemperature; set => SetField(ref _maxTemperature, value); }

        #endregion

        #region Properties - Waveform Geometry Data

        private PointCollection _voltagePoints = new();
        public PointCollection VoltagePoints
        {
            get => _voltagePoints;
            set => SetField(ref _voltagePoints, value);
        }

        private PointCollection _currentPoints = new();
        public PointCollection CurrentPoints
        {
            get => _currentPoints;
            set => SetField(ref _currentPoints, value);
        }

        private PointCollection _tempPoints = new();
        public PointCollection TempPoints
        {
            get => _tempPoints;
            set => SetField(ref _tempPoints, value);
        }

        #endregion

        #region Properties - Connection (MQTT & Serial)

        public string[] ConnectionModes { get; } = ["MQTT (Wokwi / IoT)", "SERIAL (RS-232 / COM)"];

        private string _selectedConnectionMode = "MQTT (Wokwi / IoT)";
        public string SelectedConnectionMode
        {
            get => _selectedConnectionMode;
            set
            {
                if (SetField(ref _selectedConnectionMode, value))
                {
                    OnPropertyChanged(nameof(IsMqttMode));
                    OnPropertyChanged(nameof(IsSerialMode));
                    StatusMessage = IsMqttMode 
                        ? $"Mode MQTT aktif (Broker: {MqttBroker})" 
                        : "Mode Serial Port aktif";
                }
            }
        }

        public bool IsMqttMode => SelectedConnectionMode.StartsWith("MQTT");
        public bool IsSerialMode => !IsMqttMode;

        // MQTT Options
        public ObservableCollection<string> AvailableBrokers { get; } = 
            ["broker.emqx.io", "test.mosquitto.org", "broker.hivemq.com", "localhost"];

        private string _mqttBroker = "broker.emqx.io";
        public string MqttBroker
        {
            get => _mqttBroker;
            set => SetField(ref _mqttBroker, value);
        }

        private int _mqttPort = 1883;
        public int MqttPort
        {
            get => _mqttPort;
            set => SetField(ref _mqttPort, value);
        }

        private string _mqttTopic = "sic/bms/telemetry";
        public string MqttTopic
        {
            get => _mqttTopic;
            set => SetField(ref _mqttTopic, value);
        }

        // Serial Options
        public ObservableCollection<string> AvailablePorts { get; } = new();
        public int[] BaudRates { get; } = [9600, 19200, 38400, 57600, 115200];

        private string _selectedPort = "COM1";
        public string SelectedPort
        {
            get => _selectedPort;
            set => SetField(ref _selectedPort, value);
        }

        private int _selectedBaudRate = 115200;
        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set => SetField(ref _selectedBaudRate, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetField(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionButtonText));
                    OnPropertyChanged(nameof(ConnectionStatusBadge));
                }
            }
        }

        public string ConnectionButtonText => IsConnected ? "PUTUSKAN" : "HUBUNGKAN";
        public string ConnectionStatusBadge => IsConnected 
            ? (IsMqttMode ? "TERKONEKSI (MQTT)" : "TERKONEKSI (SERIAL)") 
            : "TERPUTUS (OFFLINE)";

        private string _statusMessage = "Siap menghubungkan (Pilih MQTT untuk Wokwi atau Serial)";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        private long _packetCounter;
        public long PacketCounter
        {
            get => _packetCounter;
            set => SetField(ref _packetCounter, value);
        }

        public ObservableCollection<string> RawLogs { get; } = new();

        private bool _isDarkMode = true;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set => SetField(ref _isDarkMode, value);
        }

        private bool _isSimulationRunning;
        public bool IsSimulationRunning
        {
            get => _isSimulationRunning;
            set
            {
                if (SetField(ref _isSimulationRunning, value))
                {
                    OnPropertyChanged(nameof(SimulationButtonText));
                }
            }
        }
        public string SimulationButtonText => IsSimulationRunning ? "STOP DEMO" : "SIMULASI DEMO";

        private bool _isLogExpanded = true;
        public bool IsLogExpanded
        {
            get => _isLogExpanded;
            set
            {
                if (SetField(ref _isLogExpanded, value))
                {
                    OnPropertyChanged(nameof(LogToggleText));
                }
            }
        }
        public string LogToggleText => IsLogExpanded ? "SEMBUNYIKAN LOG ▲" : "TAMPILKAN LOG ▼";

        #endregion

        #region Methods & Handlers

        public void RefreshPorts()
        {
            AvailablePorts.Clear();
            var ports = SerialPortService.GetAvailablePorts();
            foreach (var p in ports)
            {
                AvailablePorts.Add(p);
            }

            if (AvailablePorts.Count > 0)
            {
                if (!AvailablePorts.Contains(SelectedPort))
                {
                    SelectedPort = AvailablePorts[0];
                }
            }
            else
            {
                SelectedPort = "COM1 (Virtual)";
                AvailablePorts.Add("COM1 (Virtual)");
                AvailablePorts.Add("COM2 (Virtual)");
            }
        }

        public async void ToggleConnection()
        {
            if (IsConnected)
            {
                if (_serialService.IsConnected)
                {
                    _serialService.Disconnect();
                }
                if (_mqttService.IsConnected)
                {
                    await _mqttService.DisconnectAsync();
                }
            }
            else
            {
                if (IsMqttMode)
                {
                    if (string.IsNullOrWhiteSpace(MqttBroker))
                    {
                        StatusMessage = "Masukkan alamat MQTT broker!";
                        return;
                    }
                    StatusMessage = $"Menghubungkan ke MQTT {MqttBroker}...";
                    await _mqttService.ConnectAsync(MqttBroker.Trim(), MqttPort, MqttTopic.Trim());
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(SelectedPort))
                    {
                        StatusMessage = "Pilih COM Port terlebih dahulu!";
                        return;
                    }

                    string cleanPort = SelectedPort.Split(' ')[0];
                    _serialService.Connect(cleanPort, SelectedBaudRate);
                }
            }
        }

        public void ToggleSimulation()
        {
            if (IsSimulationRunning)
            {
                _simulationTimer.Stop();
                IsSimulationRunning = false;
                StatusMessage = "Simulasi Demo dihentikan.";
            }
            else
            {
                _simulationTimer.Start();
                IsSimulationRunning = true;
                StatusMessage = "Simulasi Demo aktif (Data sensor berfluktuasi)";
            }
        }

        public void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            ApplyTheme(IsDarkMode);
        }

        public static void ApplyTheme(bool darkMode)
        {
            string themeUri = darkMode ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
            var uri = new Uri(themeUri, UriKind.Relative);
            var newDict = new ResourceDictionary { Source = uri };

            var appResources = Application.Current.Resources.MergedDictionaries;
            var existingTheme = appResources.FirstOrDefault(d => 
                d.Source != null && (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")));

            if (existingTheme != null)
            {
                appResources.Remove(existingTheme);
            }
            appResources.Add(newDict);
        }

        public void ClearLogs()
        {
            RawLogs.Clear();
        }

        private void OnIncomingRawData(string raw)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                PacketCounter++;
                RawLogs.Add($"[{DateTime.Now:HH:mm:ss.fff}] >> {raw}");
                if (RawLogs.Count > 100)
                {
                    RawLogs.RemoveAt(0);
                }
            });
        }

        private void OnIncomingTelemetryParsed(BatteryTelemetry tel)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Voltage = tel.Voltage;
                Current = tel.Current;
                Temperature = tel.Temperature;
                SOC = tel.SOC;
                CapacityAh = tel.CapacityAh;

                PushHistory(Voltage, Current, Temperature);
            });
        }

        private void OnServiceConnectionState(bool connected, string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsConnected = connected;
                StatusMessage = message;
            });
        }

        private void OnServiceError(string error)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"ERROR: {error}";
            });
        }

        private void SimulationTimer_Tick(object? sender, EventArgs e)
        {
            // Menghasilkan fluktuasi sensor yang realistis
            double deltaV = (_rand.NextDouble() - 0.48) * 0.25;
            Voltage = Math.Round(Math.Clamp(Voltage + deltaV, 44.0, 54.6), 2);

            double deltaI = (_rand.NextDouble() - 0.5) * 1.5;
            Current = Math.Round(Math.Clamp(Current + deltaI, -30.0, 30.0), 2);

            double deltaT = (_rand.NextDouble() - 0.49) * 0.15;
            Temperature = Math.Round(Math.Clamp(Temperature + deltaT, 20.0, 65.0), 1);

            // Deteksi Ah & SOC bergerak perlahan sesuai arus
            double deltaAh = (Current * 0.005);
            CapacityAh = Math.Round(Math.Clamp(CapacityAh + deltaAh, 5.0, NominalCapacityAh), 2);
            SOC = Math.Round(Math.Clamp((CapacityAh / NominalCapacityAh) * 100.0, 5.0, 100.0), 1);

            PacketCounter++;
            PushHistory(Voltage, Current, Temperature);
        }

        private void PushHistory(double v, double i, double t)
        {
            _voltageHistory.Add(v);
            _currentHistory.Add(i);
            _tempHistory.Add(t);

            if (_voltageHistory.Count > MaxHistoryPoints) _voltageHistory.RemoveAt(0);
            if (_currentHistory.Count > MaxHistoryPoints) _currentHistory.RemoveAt(0);
            if (_tempHistory.Count > MaxHistoryPoints) _tempHistory.RemoveAt(0);
        }

        private void UpdateChartPoints()
        {
            if (_voltageHistory.Count < 2) return;

            // Buat PointCollection yang dinormalkan ke viewBox 600x200
            double width = 600;
            double height = 200;
            double stepX = width / (MaxHistoryPoints - 1);

            var vPoints = new PointCollection();
            var iPoints = new PointCollection();
            var tPoints = new PointCollection();

            for (int k = 0; k < _voltageHistory.Count; k++)
            {
                double x = k * stepX;

                double vNorm = Math.Clamp((_voltageHistory[k] - 40.0) / 20.0, 0, 1);
                double yV = height - (vNorm * (height - 20) + 10);
                vPoints.Add(new Point(x, yV));

                double iNorm = Math.Clamp((_currentHistory[k] + 35.0) / 70.0, 0, 1);
                double yI = height - (iNorm * (height - 20) + 10);
                iPoints.Add(new Point(x, yI));

                double tNorm = Math.Clamp((_tempHistory[k] - 15.0) / 55.0, 0, 1);
                double yT = height - (tNorm * (height - 20) + 10);
                tPoints.Add(new Point(x, yT));
            }

            vPoints.Freeze();
            iPoints.Freeze();
            tPoints.Freeze();

            VoltagePoints = vPoints;
            CurrentPoints = iPoints;
            TempPoints = tPoints;
        }

        private void UpdateExtrema()
        {
            if (Voltage < MinVoltage || MinVoltage == 0) MinVoltage = Voltage;
            if (Voltage > MaxVoltage) MaxVoltage = Voltage;
            if (Math.Abs(Current) > MaxCurrent) MaxCurrent = Math.Abs(Current);
            if (Temperature > MaxTemperature) MaxTemperature = Temperature;
        }

        private void InitializeDummyHistory()
        {
            double baseV = 48.5;
            double baseI = 12.0;
            double baseT = 30.0;

            for (int k = 0; k < MaxHistoryPoints; k++)
            {
                double offset = Math.Sin(k * 0.15) * 1.5;
                _voltageHistory.Add(baseV + offset);
                _currentHistory.Add(baseI + offset * 2);
                _tempHistory.Add(baseT + (k * 0.05));
            }
            UpdateChartPoints();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
