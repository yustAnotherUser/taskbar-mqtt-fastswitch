using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Threading;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace TaskbarMqtt.Platform
{
    internal sealed class TrayMenuObj : ComCanonicalDbusmenuHandler
    {
        private sealed class MenuEntry
        {
            public int Id;
            public string Label;
            public Action Action;
        }

        private readonly Func<bool> _includeOpenPanel;
        private readonly Action _openPanel;
        private readonly Action _settings;
        private readonly Action _quit;

        public TrayMenuObj(Func<bool> includeOpenPanel, Action openPanel, Action settings, Action quit, Connection connection)
        {
            _includeOpenPanel = includeOpenPanel;
            _openPanel = openPanel;
            _settings = settings;
            _quit = quit;
            Connection = connection;
            Version = 4;
            TextDirection = "ltr";
            Status = "normal";
            IconThemePath = Array.Empty<string>();
        }

        public override Connection Connection { get; }

        private List<MenuEntry> Entries()
        {
            var entries = new List<MenuEntry>();
            if (_includeOpenPanel())
            {
                entries.Add(new MenuEntry { Id = 1, Label = "Open panel", Action = _openPanel });
            }
            entries.Add(new MenuEntry { Id = 2, Label = "Settings…", Action = _settings });
            entries.Add(new MenuEntry { Id = 3, Label = "Quit", Action = _quit });
            return entries;
        }

        protected override ValueTask<(uint Revision, (int, Dictionary<string, VariantValue>, VariantValue[]) Layout)> OnGetLayoutAsync(Message message, int parentId, int recursionDepth, string[] propertyNames)
        {
            var children = Entries()
                .Select(e =>
                {
                    var props = ItemProps(e.Label);
                    return VariantValue.Struct(
                        VariantValue.Int32(e.Id),
                        new Dict<string, VariantValue>(props).AsVariantValue(),
                        VariantValue.ArrayOfVariant(Array.Empty<VariantValue>()));
                })
                .ToArray();
            return ValueTask.FromResult((1u, (0, new Dictionary<string, VariantValue>(), children)));
        }

        protected override ValueTask<(int, Dictionary<string, VariantValue>)[]> OnGetGroupPropertiesAsync(Message message, int[] ids, string[] propertyNames)
        {
            var entries = Entries();
            var result = ids
                .Select(id => entries.FirstOrDefault(e => e.Id == id))
                .Where(e => e != null)
                .Select(e => (e.Id, ItemProps(e.Label)))
                .ToArray();
            return ValueTask.FromResult(result);
        }

        private static Dictionary<string, VariantValue> ItemProps(string label)
        {
            return new Dictionary<string, VariantValue>
            {
                ["label"] = VariantValue.String(label),
                ["enabled"] = VariantValue.Bool(true),
                ["visible"] = VariantValue.Bool(true)
            };
        }

        protected override ValueTask<VariantValue> OnGetPropertyAsync(Message message, int id, string name)
        {
            var entry = Entries().FirstOrDefault(e => e.Id == id);
            if (entry == null)
                return ValueTask.FromResult(VariantValue.Int32(0));

            switch (name)
            {
                case "label":
                    return ValueTask.FromResult(VariantValue.String(entry.Label));
                case "enabled":
                case "visible":
                    return ValueTask.FromResult(VariantValue.Bool(true));
                case "type":
                    return ValueTask.FromResult(VariantValue.String("standard"));
                default:
                    return ValueTask.FromResult(VariantValue.Int32(0));
            }
        }

        protected override ValueTask OnEventAsync(Message message, int id, string eventId, VariantValue data, uint timestamp)
        {
            if (eventId == "clicked")
            {
                var entry = Entries().FirstOrDefault(e => e.Id == id);
                if (entry != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        try { entry.Action(); } catch { }
                    });
                }
            }
            return ValueTask.CompletedTask;
        }

        protected override ValueTask<int[]> OnEventGroupAsync(Message message, (int, string, VariantValue, uint)[] events)
        {
            foreach (var (id, eventId, data, timestamp) in events)
            {
                if (eventId == "clicked")
                {
                    OnEventAsync(message, id, eventId, data, timestamp);
                }
            }
            return ValueTask.FromResult(Array.Empty<int>());
        }

        protected override ValueTask<bool> OnAboutToShowAsync(Message message, int id)
        {
            return ValueTask.FromResult(false);
        }

        protected override ValueTask<(int[] UpdatesNeeded, int[] IdErrors)> OnAboutToShowGroupAsync(Message message, int[] ids)
        {
            return ValueTask.FromResult((Array.Empty<int>(), Array.Empty<int>()));
        }

    }
}
