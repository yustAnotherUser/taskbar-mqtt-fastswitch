using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using TaskbarMqtt.Config;

namespace TaskbarMqtt.Mqtt
{
    public class MqttService : IDisposable
    {
        private IMqttClient _client;
        private BrokerSettings _broker;
        private CancellationTokenSource _reconnectCts;
        private int _reconnectAttempt;
        private bool _disposed;
        private readonly object _gate = new object();

        public event Action<string> StatusChanged;
        public event Action<string> Error;

        public bool IsConnected => _client != null && _client.IsConnected;
        public string CurrentClientId { get; private set; } = "";

        public void UpdateConfig(BrokerSettings broker)
        {
            lock (_gate)
            {
                _broker = broker ?? new BrokerSettings();
            }
            _ = Task.Run(() => ReconnectAsync());
        }

        public async Task StartAsync()
        {
            await ReconnectAsync().ConfigureAwait(false);
        }

        public async Task ReconnectAsync()
        {
            await ConnectInternalAsync().ConfigureAwait(false);
        }

        private async Task ConnectInternalAsync()
        {
            BrokerSettings snapshot;
            lock (_gate) { snapshot = _broker; }
            if (snapshot == null) return;

            if (string.IsNullOrWhiteSpace(snapshot.Host))
            {
                StatusChanged?.Invoke("No broker configured");
                return;
            }

            try
            {
                if (_client == null)
                {
                    var factory = new MqttFactory();
                    _client = factory.CreateMqttClient();
                    _client.DisconnectedAsync += OnDisconnectedAsync;
                }

                if (_client.IsConnected) return;

                var suffix = new Random().Next(100000, 999999).ToString();
                CurrentClientId = (!string.IsNullOrWhiteSpace(snapshot.ClientId)
                    ? snapshot.ClientId
                    : "TaskbarMqttClient-" + Environment.MachineName) + "-" + suffix;

                var builder = new MqttClientOptionsBuilder()
                    .WithTcpServer(snapshot.Host, snapshot.Port)
                    .WithClientId(CurrentClientId)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(snapshot.KeepAliveSeconds > 0 ? snapshot.KeepAliveSeconds : 30))
                    .WithTimeout(TimeSpan.FromSeconds(snapshot.ConnectTimeoutSeconds > 0 ? snapshot.ConnectTimeoutSeconds : 10))
                    .WithCleanSession();

                if (!string.IsNullOrEmpty(snapshot.Username))
                {
                    var pwd = snapshot.Password ?? "";
                    builder = builder.WithCredentials(snapshot.Username, pwd);
                }

                if (snapshot.UseTls)
                {
                    builder = builder.WithTlsOptions(o =>
                    {
                        if (snapshot.AllowInvalidCerts)
                        {
                            o.WithCertificateValidationHandler(c => true);
                            o.WithAllowUntrustedCertificates(true);
                        }
                    });
                }

                var options = builder.Build();
                StatusChanged?.Invoke("Connecting to " + snapshot.Host + ":" + snapshot.Port + "…");
                await _client.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);

                if (_client.IsConnected)
                {
                    _reconnectAttempt = 0;
                    StatusChanged?.Invoke("Connected");
                }
                else
                {
                    StatusChanged?.Invoke("Not connected");
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("Connection failed: " + ex.Message);
                Error?.Invoke(ex.Message);
            }
        }

        private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            StatusChanged?.Invoke("Disconnected: " + e.Reason);
            ScheduleReconnect();
            await Task.CompletedTask;
        }

        private void ScheduleReconnect()
        {
            if (_disposed) return;

            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            _reconnectAttempt = Math.Min(_reconnectAttempt + 1, 6);
            var delaySec = (int)Math.Min(30, Math.Pow(2, _reconnectAttempt));
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), token).ConfigureAwait(false);
                    await ConnectInternalAsync().ConfigureAwait(false);
                }
                catch (TaskCanceledException) { }
            });
        }

        public async Task<bool> PublishAsync(ButtonConfig button)
        {
            if (button == null) return false;
            if (string.IsNullOrEmpty(button.Topic)) return false;

            try
            {
                if (_client == null || !_client.IsConnected)
                {
                    await ConnectInternalAsync().ConfigureAwait(false);
                }

                if (_client == null || !_client.IsConnected)
                {
                    Error?.Invoke("Not connected to broker");
                    return false;
                }

                var qos = (MqttQualityOfServiceLevel)Math.Max(0, Math.Min(2, button.Qos));
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(button.Topic)
                    .WithPayload(button.Payload ?? "")
                    .WithQualityOfServiceLevel(qos)
                    .WithRetainFlag(button.Retain)
                    .Build();

                await _client.PublishAsync(message, CancellationToken.None).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke("Publish failed: " + ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            try { _reconnectCts?.Cancel(); } catch { }
            try
            {
                if (_client != null)
                {
                    _client.DisconnectedAsync -= OnDisconnectedAsync;
                    if (_client.IsConnected)
                    {
                        try { _client.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
                    }
                    _client.Dispose();
                }
            }
            catch { }
            _client = null;
        }
    }
}
