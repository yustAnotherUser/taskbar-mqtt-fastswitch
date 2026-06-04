using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TaskbarMqtt.Config
{
    public enum DisplayMode
    {
        PopupPanel = 0,
        MultipleIcons = 1
    }

    public class BrokerSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 1883;
        public bool UseTls { get; set; } = false;
        public bool AllowInvalidCerts { get; set; } = true;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string ClientId { get; set; } = "";
        public int KeepAliveSeconds { get; set; } = 30;
    }

    public class ButtonConfig
    {
        public string Label { get; set; } = "";
        public string Topic { get; set; } = "";
        public string Payload { get; set; } = "";
        public int Qos { get; set; } = 0;
        public bool Retain { get; set; } = false;
        public string IconPath { get; set; } = "";
    }

    public class AppConfig
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public DisplayMode DisplayMode { get; set; } = DisplayMode.PopupPanel;

        public int ButtonCount { get; set; } = 4;
        public bool StartWithWindows { get; set; } = false;
        public BrokerSettings Broker { get; set; } = new BrokerSettings();
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();

        public const int MinButtons = 1;
        public const int MaxButtons = 9;

        public static AppConfig CreateDefault()
        {
            var cfg = new AppConfig();
            for (int i = 0; i < cfg.ButtonCount; i++)
            {
                cfg.Buttons.Add(new ButtonConfig
                {
                    Label = "Button " + (i + 1),
                    Topic = "",
                    Payload = ""
                });
            }
            return cfg;
        }

        public void Normalize()
        {
            if (ButtonCount < MinButtons) ButtonCount = MinButtons;
            if (ButtonCount > MaxButtons) ButtonCount = MaxButtons;

            while (Buttons.Count < ButtonCount) Buttons.Add(new ButtonConfig());
            while (Buttons.Count > ButtonCount) Buttons.RemoveAt(Buttons.Count - 1);

            if (Broker == null) Broker = new BrokerSettings();
            if (Broker.KeepAliveSeconds <= 0) Broker.KeepAliveSeconds = 30;
        }
    }
}
