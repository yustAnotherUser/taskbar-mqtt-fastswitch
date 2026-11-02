using System;
using System.Runtime.InteropServices;

namespace TaskbarMqtt.Platform
{
    public static class PointerPosition
    {
        [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6", EntryPoint = "XDefaultRootWindow")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11.so.6", EntryPoint = "XQueryPointer")]
        private static extern int XQueryPointer(IntPtr display, IntPtr window, out IntPtr rootReturn,
            out IntPtr childReturn, out int rootXReturn, out int rootYReturn,
            out int winXReturn, out int winYReturn, out int maskReturn);

        public static bool TryGet(out int x, out int y)
        {
            x = 0;
            y = 0;
            try
            {
                var display = XOpenDisplay(IntPtr.Zero);
                if (display == IntPtr.Zero) return false;
                try
                {
                    var root = XDefaultRootWindow(display);
                    if (XQueryPointer(display, root, out _, out _, out int rx, out int ry, out _, out _, out _) != 0)
                    {
                        x = rx;
                        y = ry;
                        return true;
                    }
                }
                finally
                {
                    XCloseDisplay(display);
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
