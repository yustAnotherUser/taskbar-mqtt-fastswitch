using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using SkiaSharp;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace TaskbarMqtt.Platform
{
    public class TrayIconService : IDisposable
    {
        public event Action<int, int, int> ItemActivated;
        public event Action<int, int, int> ItemContextMenu;
        public event Action OpenPanelRequested;
        public event Action SettingsRequested;
        public event Action QuitRequested;

        private readonly Func<bool> _includeOpenPanel;

        public TrayIconService(Func<bool> includeOpenPanel)
        {
            _includeOpenPanel = includeOpenPanel;
        }

        public const int MaxVisibleIcons = 9;
        private const int Capacity = 12;

        private readonly ItemEntry[] _items = new ItemEntry[Capacity];
        private DispatcherTimer _reRegisterTimer;
        private bool _disposed;

        private sealed class ItemEntry
        {
            public Connection Connection;
            public PathHandler PathHandler;
            public StatusNotifierItemObj Handler;
            public string BusName;
        }

        public Task StartAsync()
        {
            _reRegisterTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            _reRegisterTimer.Tick += async (s, e) => await ReRegisterAllAsync();
            _reRegisterTimer.Start();
            return Task.CompletedTask;
        }

        public async Task SetItemAsync(int index, SKBitmap icon, string title, string description, bool active)
        {
            if (index < 0 || index >= Capacity) return;

            if (!active)
            {
                if (_items[index] != null)
                {
                    await DestroyItemAsync(index);
                }
                return;
            }

            if (_items[index] == null)
            {
                try
                {
                    _items[index] = await CreateItemAsync(index);
                }
                catch
                {
                    return;
                }
            }

            _items[index].Handler.Update(icon, title, description);
        }

        private async Task<ItemEntry> CreateItemAsync(int index)
        {
            var connection = new Connection(Address.Session);
            await connection.ConnectAsync();

            var pathHandler = new PathHandler("/StatusNotifierItem");
            var handler = new StatusNotifierItemObj(this, index, connection, new ObjectPath("/menu"));
            pathHandler.Add(handler);
            connection.AddMethodHandler(pathHandler);

            var menuPathHandler = new PathHandler("/menu");
            var menuObj = new TrayMenuObj(_includeOpenPanel,
                () => OpenPanelRequested?.Invoke(),
                () => SettingsRequested?.Invoke(),
                () => QuitRequested?.Invoke(),
                connection);
            menuPathHandler.Add(menuObj);
            connection.AddMethodHandler(menuPathHandler);

            var busName = "org.kde.StatusNotifierItem.taskbar_mqtt_" + Environment.ProcessId + "_" + index;
            var dbus = new OrgFreedesktopDBusProxy(connection, "org.freedesktop.DBus", new ObjectPath("/org/freedesktop/DBus"));
            await dbus.RequestNameAsync(busName, 0);

            var watcher = new OrgKdeStatusNotifierWatcherProxy(connection, "org.kde.StatusNotifierWatcher", new ObjectPath("/StatusNotifierWatcher"));
            await watcher.RegisterStatusNotifierItemAsync(busName);

            return new ItemEntry { Connection = connection, PathHandler = pathHandler, Handler = handler, BusName = busName };
        }

        private Task DestroyItemAsync(int index)
        {
            var item = _items[index];
            _items[index] = null;
            if (item == null) return Task.CompletedTask;
            try { item.Connection.RemoveMethodHandler(item.PathHandler.Path); } catch { }
            try { item.Connection.Dispose(); } catch { }
            return Task.CompletedTask;
        }

        private async Task ReRegisterAllAsync()
        {
            if (_disposed) return;
            for (int i = 0; i < Capacity; i++)
            {
                var item = _items[i];
                if (item == null) continue;
                try
                {
                    var watcher = new OrgKdeStatusNotifierWatcherProxy(item.Connection, "org.kde.StatusNotifierWatcher", new ObjectPath("/StatusNotifierWatcher"));
                    await watcher.RegisterStatusNotifierItemAsync(item.BusName);
                }
                catch
                {
                }
            }
        }

        internal void FireActivated(int index, int x, int y)
        {
            Dispatcher.UIThread.Post(() => ItemActivated?.Invoke(index, x, y));
        }

        internal void FireContextMenu(int index, int x, int y)
        {
            Dispatcher.UIThread.Post(() => ItemContextMenu?.Invoke(index, x, y));
        }

        internal static (int, int, byte[]) EncodeArgb32(SKBitmap bmp)
        {
            var bytes = new byte[bmp.Width * bmp.Height * 4];
            var pixels = bmp.Bytes;
            int o = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                bytes[o] = pixels[i + 3];
                bytes[o + 1] = pixels[i + 2];
                bytes[o + 2] = pixels[i + 1];
                bytes[o + 3] = pixels[i];
                o += 4;
            }
            return (bmp.Width, bmp.Height, bytes);
        }

        public void Dispose()
        {
            _disposed = true;
            try { _reRegisterTimer?.Stop(); } catch { }
            for (int i = 0; i < Capacity; i++)
            {
                try
                {
                    _items[i]?.Connection?.Dispose();
                }
                catch
                {
                }
                _items[i] = null;
            }
        }
    }

    internal sealed class StatusNotifierItemObj : OrgKdeStatusNotifierItemHandler
    {
        private readonly TrayIconService _owner;
        private readonly int _index;

        public override Connection Connection { get; }

        public StatusNotifierItemObj(TrayIconService owner, int index, Connection connection, ObjectPath menuPath)
        {
            _owner = owner;
            _index = index;
            Connection = connection;
            Menu = menuPath;
            Id = "Taskbar MQTT";
            Category = "ApplicationStatus";
            Status = "Active";
            IconName = "";
            IconThemePath = "";
            ItemIsMenu = false;
            WindowId = 0;
            IconPixmap = new[] { TrayIconService.EncodeArgb32(new SKBitmap(1, 1)) };
        }

        protected override ValueTask OnActivateAsync(Message message, int x, int y)
        {
            _owner.FireActivated(_index, x, y);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnSecondaryActivateAsync(Message message, int x, int y)
        {
            _owner.FireActivated(_index, x, y);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnContextMenuAsync(Message message, int x, int y)
        {
            _owner.FireContextMenu(_index, x, y);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnScrollAsync(Message message, int delta, string orientation)
        {
            return ValueTask.CompletedTask;
        }

        public void Update(SKBitmap icon, string title, string description)
        {
            var pixmap = TrayIconService.EncodeArgb32(icon);
            Id = "Taskbar MQTT";
            Category = "ApplicationStatus";
            Status = "Active";
            Title = title ?? "Taskbar MQTT";
            IconPixmap = new[] { pixmap };
            ToolTip = ("", new[] { pixmap }, Title, description ?? "");
            EmitNewTitle();
            EmitNewIcon();
            EmitNewToolTip();
            EmitNewStatus(Status);
        }
    }
}
