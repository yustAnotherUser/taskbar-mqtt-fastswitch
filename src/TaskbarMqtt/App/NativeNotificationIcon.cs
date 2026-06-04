using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TaskbarMqtt.App
{
    internal static class NativeNotifyIconNative
    {
        public const uint NIM_ADD = 0;
        public const uint NIM_MODIFY = 1;
        public const uint NIM_DELETE = 2;
        public const uint NIM_SETVERSION = 4;

        public const uint NIF_MESSAGE = 0x01;
        public const uint NIF_ICON = 0x02;
        public const uint NIF_TIP = 0x04;
        public const uint NIF_INFO = 0x10;
        public const uint NIF_GUID = 0x20;

        public const uint NOTIFYICON_VERSION_4 = 4;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool Shell_NotifyIconW(uint cmd, ref NativeNotifyIconData data);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeNotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
    }

    internal class NativeNotifyIconManager : IDisposable
    {
        private sealed class CallbackWindow : NativeWindow
        {
            private readonly NativeNotifyIconManager _owner;
            private readonly uint _msgId;

            public CallbackWindow(NativeNotifyIconManager owner, uint msgId)
            {
                _owner = owner;
                _msgId = msgId;
                CreateHandle(new CreateParams
                {
                    Caption = "TaskbarMqttNotifyIcon",
                    Style = 0,
                    ExStyle = 0,
                    Parent = (IntPtr)(-3)
                });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == _msgId)
                {
                    uint iconId = (uint)(int)m.WParam;
                    uint msg = (uint)(int)m.LParam;

                    switch (msg)
                    {
                        case 0x0202:
                            _owner.OnIconLeftClick(iconId);
                            return;
                        case 0x0205:
                            _owner.OnIconRightClick(iconId);
                            return;
                    }
                }
                base.WndProc(ref m);
            }
        }

        private static uint _globalCallbackMsg = 0x8000;
        private static readonly object _lock = new object();

        private readonly CallbackWindow _window;
        private readonly uint _callbackMsg;
        private readonly Dictionary<uint, uint> _idToIndex = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, uint> _indexToId = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, Guid> _indexToGuid = new Dictionary<uint, Guid>();
        private uint _nextId = 1;
        private bool _disposed;

        public NativeNotifyIconManager()
        {
            lock (_lock)
            {
                _callbackMsg = _globalCallbackMsg++;
            }
            _window = new CallbackWindow(this, _callbackMsg);
        }

        public IntPtr CallbackHandle => _window.Handle;

        public event Action<uint> IconLeftClick;
        public event Action<uint> IconRightClick;

        public bool Add(uint index, Guid guid, Icon icon, string tooltip)
        {
            if (_disposed) return false;

            uint id = _nextId++;
            _idToIndex[id] = index;
            _indexToId[index] = id;
            _indexToGuid[index] = guid;

            var data = new NativeNotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeNotifyIconData)),
                hWnd = _window.Handle,
                uID = id,
                uFlags = NativeNotifyIconNative.NIF_MESSAGE | NativeNotifyIconNative.NIF_ICON | NativeNotifyIconNative.NIF_TIP | NativeNotifyIconNative.NIF_GUID,
                uCallbackMessage = _callbackMsg,
                hIcon = icon != null ? icon.Handle : IntPtr.Zero,
                szTip = TruncateTip(tooltip),
                guidItem = guid
            };

            if (!NativeNotifyIconNative.Shell_NotifyIconW(NativeNotifyIconNative.NIM_ADD, ref data))
                return false;

            data.uVersion = NativeNotifyIconNative.NOTIFYICON_VERSION_4;
            NativeNotifyIconNative.Shell_NotifyIconW(NativeNotifyIconNative.NIM_SETVERSION, ref data);

            return true;
        }

        public bool Remove(uint index)
        {
            if (_disposed) return false;
            if (!_indexToId.TryGetValue(index, out var id)) return false;
            if (!_indexToGuid.TryGetValue(index, out var guid)) return false;

            var data = new NativeNotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeNotifyIconData)),
                hWnd = _window.Handle,
                uID = id,
                uFlags = NativeNotifyIconNative.NIF_GUID,
                guidItem = guid
            };

            NativeNotifyIconNative.Shell_NotifyIconW(NativeNotifyIconNative.NIM_DELETE, ref data);

            _idToIndex.Remove(id);
            _indexToId.Remove(index);
            _indexToGuid.Remove(index);
            return true;
        }

        public void Clear()
        {
            var indices = new List<uint>(_indexToId.Keys);
            foreach (var idx in indices)
                Remove(idx);
        }

        public bool Modify(uint index, Icon icon, string tooltip)
        {
            if (_disposed) return false;
            if (!_indexToId.TryGetValue(index, out var id)) return false;
            if (!_indexToGuid.TryGetValue(index, out var guid)) return false;

            var data = new NativeNotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeNotifyIconData)),
                hWnd = _window.Handle,
                uID = id,
                uFlags = NativeNotifyIconNative.NIF_ICON | NativeNotifyIconNative.NIF_TIP | NativeNotifyIconNative.NIF_GUID,
                hIcon = icon != null ? icon.Handle : IntPtr.Zero,
                szTip = TruncateTip(tooltip),
                guidItem = guid
            };

            return NativeNotifyIconNative.Shell_NotifyIconW(NativeNotifyIconNative.NIM_MODIFY, ref data);
        }

        public bool ShowBalloon(uint index, string title, string text, ToolTipIcon icon)
        {
            if (_disposed) return false;
            if (!_indexToId.TryGetValue(index, out var id)) return false;
            if (!_indexToGuid.TryGetValue(index, out var guid)) return false;

            uint flags;
            switch (icon)
            {
                case ToolTipIcon.Info: flags = 1; break;
                case ToolTipIcon.Warning: flags = 2; break;
                case ToolTipIcon.Error: flags = 3; break;
                default: flags = 0; break;
            }

            var data = new NativeNotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeNotifyIconData)),
                hWnd = _window.Handle,
                uID = id,
                uFlags = NativeNotifyIconNative.NIF_INFO | NativeNotifyIconNative.NIF_GUID,
                szInfo = text ?? "",
                szInfoTitle = title ?? "",
                dwInfoFlags = flags,
                guidItem = guid
            };

            return NativeNotifyIconNative.Shell_NotifyIconW(NativeNotifyIconNative.NIM_MODIFY, ref data);
        }

        private void OnIconLeftClick(uint id)
        {
            if (_idToIndex.TryGetValue(id, out var index))
                IconLeftClick?.Invoke(index);
        }

        private void OnIconRightClick(uint id)
        {
            if (_idToIndex.TryGetValue(id, out var index))
                IconRightClick?.Invoke(index);
        }

        private static string TruncateTip(string tip)
        {
            if (tip == null) return "";
            return tip.Length > 127 ? tip.Substring(0, 127) : tip;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
            _window.DestroyHandle();
        }
    }
}
