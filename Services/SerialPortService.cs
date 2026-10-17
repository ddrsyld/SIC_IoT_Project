using System;
using System.Globalization;
using System.IO.Ports;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashboardMonitorBateraiManajemen.Models;

namespace DashboardMonitorBateraiManajemen.Services
{
    public class SerialPortService : IDisposable
    {
        private SerialPort? _serialPort;
        private readonly object _lock = new();

        public event Action<string>? OnRawDataReceived;
        public event Action<BatteryTelemetry>? OnTelemetryParsed;
        public event Action<bool, string>? OnConnectionStateChanged;
        public event Action<string>? OnError;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;
        public string CurrentPortName => _serialPort?.PortName ?? string.Empty;

        public static string[] GetAvailablePorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                Array.Sort(ports);
                return ports;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public bool Connect(string portName, int baudRate = 115200)
        {
            lock (_lock)
            {
                Disconnect();

                try
                {
                    _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 1000,
                        NewLine = "\n",
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    _serialPort.DataReceived += HandleDataReceived;
                    _serialPort.Open();

                    OnConnectionStateChanged?.Invoke(true, $"Terkoneksi ke {portName} @ {baudRate} bps");
                    return true;
                }
                catch (Exception ex)
                {
                    OnConnectionStateChanged?.Invoke(false, $"Gagal koneksi ke {portName}: {ex.Message}");
                    OnError?.Invoke(ex.Message);
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.DataReceived -= HandleDataReceived;
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(ex.Message);
                    }
                    finally
                    {
                        _serialPort = null;
                        OnConnectionStateChanged?.Invoke(false, "Terputus dari Serial Port");
                    }
                }
            }
        }

        public bool SendData(string command)
        {
            lock (_lock)
            {
                if (IsConnected && _serialPort != null)
                {
                    try
                    {
                        _serialPort.WriteLine(command);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke($"Gagal kirim perintah: {ex.Message}");
                    }
                }
                return false;
            }
        }

        private void HandleDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                while (_serialPort.BytesToRead > 0)
                {
                    string rawLine = _serialPort.ReadLine().Trim();
                    if (string.IsNullOrWhiteSpace(rawLine)) continue;

                    OnRawDataReceived?.Invoke(rawLine);

                    var telemetry = ParseTelemetryData(rawLine);
                    if (telemetry != null)
                    {
                        OnTelemetryParsed?.Invoke(telemetry);
                    }
                }
            }
            catch (TimeoutException)
            {
                // Timeout biasa terjadi saat buffer belum lengkap, abaikan
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error baca serial: {ex.Message}");
            }
        }

        /// <summary>
        /// Parsing data cerdas mendukung 3 format umum: JSON, Key-Value, dan CSV
        /// </summary>
        public static BatteryTelemetry? ParseTelemetryData(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = raw.Trim();

            // 1. Coba Parsing JSON: {"v": 48.2, "i": 12.5, "t": 32.1, "soc": 87, "soh": 98}
            if (raw.StartsWith("{") && raw.EndsWith("}"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;

                    double v = GetJsonDouble(root, "v", "voltage", "tegangan");
                    double i = GetJsonDouble(root, "i", "current", "arus");
                    double t = GetJsonDouble(root, "t", "temp", "temperature", "suhu");
                    double soc = GetJsonDouble(root, "soc", "charge");
                    double ah = GetJsonDouble(root, "ah", "cap", "capacity", "ampere_hour", "soh");
                    if (ah <= 0 && soc > 0) ah = Math.Round((soc / 100.0) * 100.0, 1);

                    return new BatteryTelemetry
                    {
                        Voltage = Math.Round(v, 2),
                        Current = Math.Round(i, 2),
                        Temperature = Math.Round(t, 1),
                        SOC = Math.Clamp(Math.Round(soc, 1), 0, 100),
                        CapacityAh = Math.Round(ah, 2),
                        Timestamp = DateTime.Now
                    };
                }
                catch
                {
                    // Bukan JSON valid, lanjutkan ke parser lain
                }
            }

            // 2. Coba Parsing Key-Value: V:48.2,I:12.5,T:32.1,SOC:87,AH:87 atau V=48.2;I=...
            if (raw.Contains(':') || raw.Contains('='))
            {
                try
                {
                    double v = ExtractRegexNumber(raw, @"(?:V|VOLTAGE|TEGANGAN)\s*[:=]\s*([+-]?\d+(?:\.\d+)?)");
                    double i = ExtractRegexNumber(raw, @"(?:I|CURRENT|ARUS)\s*[:=]\s*([+-]?\d+(?:\.\d+)?)");
                    double t = ExtractRegexNumber(raw, @"(?:T|TEMP|TEMPERATURE|SUHU)\s*[:=]\s*([+-]?\d+(?:\.\d+)?)");
                    double soc = ExtractRegexNumber(raw, @"(?:SOC|CHARGE)\s*[:=]\s*([+-]?\d+(?:\.\d+)?)");
                    double ah = ExtractRegexNumber(raw, @"(?:AH|CAP|CAPACITY|AMPERE_HOUR|SOH)\s*[:=]\s*([+-]?\d+(?:\.\d+)?)");
                    if (ah <= 0 && soc > 0) ah = Math.Round((soc / 100.0) * 100.0, 1);

                    // Minimal salah satu parameter penting ditemukan
                    if (v > 0 || soc > 0 || ah > 0)
                    {
                        return new BatteryTelemetry
                        {
                            Voltage = Math.Round(v, 2),
                            Current = Math.Round(i, 2),
                            Temperature = Math.Round(t, 1),
                            SOC = Math.Clamp(Math.Round(soc, 1), 0, 100),
                            CapacityAh = Math.Round(ah, 2),
                            Timestamp = DateTime.Now
                        };
                    }
                }
                catch
                {
                    // Lanjutkan ke CSV parser
                }
            }

            // 3. Coba Parsing CSV: 48.2,12.5,32.1,87,87 (V, I, T, SOC, AH)
            char delimiter = raw.Contains(';') ? ';' : ',';
            var parts = raw.Split(delimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 5)
            {
                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double v) &&
                    double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double i) &&
                    double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double t) &&
                    double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double soc) &&
                    double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double ah))
                {
                    return new BatteryTelemetry
                    {
                        Voltage = Math.Round(v, 2),
                        Current = Math.Round(i, 2),
                        Temperature = Math.Round(t, 1),
                        SOC = Math.Clamp(Math.Round(soc, 1), 0, 100),
                        CapacityAh = Math.Round(ah, 2),
                        Timestamp = DateTime.Now
                    };
                }
            }

            return null;
        }

        private static double GetJsonDouble(JsonElement root, params string[] propertyNames)
        {
            foreach (var prop in propertyNames)
            {
                if (root.TryGetProperty(prop, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number && val.TryGetDouble(out double d)) return d;
                    if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double ds)) return ds;
                }

                // Cek uppercase / lowercase
                foreach (var jsonProp in root.EnumerateObject())
                {
                    if (jsonProp.Name.Equals(prop, StringComparison.OrdinalIgnoreCase))
                    {
                        if (jsonProp.Value.ValueKind == JsonValueKind.Number && jsonProp.Value.TryGetDouble(out double d)) return d;
                        if (jsonProp.Value.ValueKind == JsonValueKind.String && double.TryParse(jsonProp.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double ds)) return ds;
                    }
                }
            }
            return 0;
        }

        private static double ExtractRegexNumber(string input, string pattern)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                if (double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }
            }
            return 0;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
