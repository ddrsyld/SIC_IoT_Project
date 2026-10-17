using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using DashboardMonitorBateraiManajemen.Models;

namespace DashboardMonitorBateraiManajemen.Services
{
    public class MqttService : IDisposable
    {
        private IMqttClient? _mqttClient;
        private readonly MqttClientFactory _mqttFactory = new();

        public event Action<string>? OnRawDataReceived;
        public event Action<BatteryTelemetry>? OnTelemetryParsed;
        public event Action<bool, string>? OnConnectionStateChanged;
        public event Action<string>? OnError;

        public bool IsConnected => _mqttClient != null && _mqttClient.IsConnected;
        public string CurrentBroker { get; private set; } = string.Empty;
        public string CurrentTopic { get; private set; } = string.Empty;

        public async Task<bool> ConnectAsync(string brokerHost, int port = 1883, string topic = "sic/bms/telemetry", string? clientId = null)
        {
            try
            {
                await DisconnectAsync();

                CurrentBroker = brokerHost;
                CurrentTopic = topic;

                _mqttClient = _mqttFactory.CreateMqttClient();

                clientId ??= $"BmsDashboard_{Guid.NewGuid().ToString("N")[..8]}";

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(brokerHost, port)
                    .WithClientId(clientId)
                    .WithCleanSession()
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .Build();

                _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageReceivedAsync;
                _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;

                var connectResult = await _mqttClient.ConnectAsync(options, CancellationToken.None);

                if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
                {
                    // Subscribe to topic
                    var subscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(topic))
                        .Build();

                    await _mqttClient.SubscribeAsync(subscribeOptions, CancellationToken.None);

                    OnConnectionStateChanged?.Invoke(true, $"Terkoneksi ke MQTT: {brokerHost}:{port} | Topic: {topic}");
                    return true;
                }
                else
                {
                    OnConnectionStateChanged?.Invoke(false, $"Gagal koneksi MQTT: {connectResult.ReasonString}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnConnectionStateChanged?.Invoke(false, $"Error MQTT: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_mqttClient != null)
            {
                try
                {
                    if (_mqttClient.IsConnected)
                    {
                        await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build());
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex.Message);
                }
                finally
                {
                    _mqttClient.Dispose();
                    _mqttClient = null;
                    OnConnectionStateChanged?.Invoke(false, "Terputus dari Broker MQTT");
                }
            }
        }

        private Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                string payload = Encoding.UTF8.GetString(BuffersExtensions.ToArray(e.ApplicationMessage.Payload));
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    OnRawDataReceived?.Invoke(payload);

                    var telemetry = SerialPortService.ParseTelemetryData(payload);
                    if (telemetry != null)
                    {
                        OnTelemetryParsed?.Invoke(telemetry);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error proses payload MQTT: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            OnConnectionStateChanged?.Invoke(false, $"MQTT Terputus: {e.Reason}");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
        }
    }
}
