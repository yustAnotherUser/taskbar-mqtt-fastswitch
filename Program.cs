using System;
using System.Threading;
using System.Windows.Forms;
using TaskbarMqtt.App;
using TaskbarMqtt.Config;

namespace TaskbarMqtt
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            const string mutexName = "TaskbarMqtt.SingleInstance";
            const string showEventName = "TaskbarMqtt.ShowRequest";

            bool createdNew;
            using (var mutex = new Mutex(true, mutexName, out createdNew))
            {
                if (!createdNew)
                {
                    try
                    {
                        using (var ev = EventWaitHandle.OpenExisting(showEventName))
                        {
                            ev.Set();
                        }
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => { /* swallow in tray app */ };

                var config = ConfigStore.Load();
                bool firstRun = !System.IO.File.Exists(ConfigStore.ConfigPath);
                if (firstRun || config.ButtonCount < AppConfig.MinButtons || config.ButtonCount > AppConfig.MaxButtons)
                {
                    config.ButtonCount = 4;
                    config.Normalize();
                    ConfigStore.Save(config);
                }

                var tray = new TrayContext(config);
                using (var showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName))
                {
                    var watcher = new Thread(() =>
                    {
                        while (showEvt.WaitOne())
                        {
                            try
                            {
                                tray.PostToUiThread(tray.OnExternalShowRequest);
                            }
                            catch { }
                        }
                    }) { IsBackground = true, Name = "TaskbarMqtt.SecondInstanceWatcher" };
                    watcher.Start();

                    Application.Run(tray);
                }
            }
        }
    }
}
