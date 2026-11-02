using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Threading;

namespace TaskbarMqtt.Platform
{
    public static class SingleInstance
    {
        private static Socket _listener;
        private static string _socketPath;

        public static string SocketPath
        {
            get
            {
                if (_socketPath != null) return _socketPath;
                var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                if (string.IsNullOrEmpty(runtimeDir))
                    runtimeDir = Path.GetTempPath();
                _socketPath = Path.Combine(runtimeDir, "taskbar-mqtt.sock");
                return _socketPath;
            }
        }

        public static bool IsPrimary()
        {
            try
            {
                using (var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                {
                    probe.Connect(new UnixDomainSocketEndPoint(SocketPath));
                    var payload = Encoding.UTF8.GetBytes("show");
                    probe.Send(payload);
                    probe.Close();
                    return false;
                }
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            try
            {
                if (File.Exists(SocketPath))
                    File.Delete(SocketPath);
            }
            catch
            {
            }
            return true;
        }

        public static void StartListening(Action onShowRequest)
        {
            try
            {
                _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
                _listener.Listen(2);
                var thread = new Thread(() => AcceptLoop(onShowRequest))
                {
                    IsBackground = true,
                    Name = "TaskbarMqtt.SingleInstanceListener"
                };
                thread.Start();
            }
            catch
            {
                _listener = null;
            }
        }

        private static void AcceptLoop(Action onShowRequest)
        {
            while (_listener != null)
            {
                Socket client;
                try
                {
                    client = _listener.Accept();
                }
                catch
                {
                    return;
                }
                try
                {
                    using (client)
                    {
                        var buffer = new byte[32];
                        client.Receive(buffer);
                    }
                    Dispatcher.UIThread.Post(() =>
                    {
                        try { onShowRequest?.Invoke(); } catch { }
                    });
                }
                catch
                {
                }
            }
        }
    }
}
