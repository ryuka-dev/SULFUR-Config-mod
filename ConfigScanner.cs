using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Ryuka.Sulfur.NativeUI;

namespace Ryuka.SulfurConfig
{
    internal static class ConfigScanner
    {
        public static List<ConfigPluginGroup> Scan(ManualLogSource logger)
        {
            List<ConfigPluginGroup> result = new List<ConfigPluginGroup>();

            foreach (PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
            {
                if (pluginInfo == null || pluginInfo.Instance == null || pluginInfo.Metadata == null)
                    continue;

                ConfigFile config = pluginInfo.Instance.Config;
                if (config == null)
                    continue;

                string guid = pluginInfo.Metadata.GUID ?? "unknown.guid";
                string name = pluginInfo.Metadata.Name ?? guid;
                string location = pluginInfo.Location ?? "";

                // Each mod owns its own lang/*.json folder.
                // SULFUR Config only loads and reads the target mod's localization files.
                SulfurLocalization.LoadPluginLocalization(guid, location);

                string localizedName = SulfurLocalization.Get(guid, "plugin.name", name);
                string localizedDescription = SulfurLocalization.Get(guid, "plugin.description", "");

                ConfigPluginGroup plugin = new ConfigPluginGroup
                {
                    Guid = guid,
                    Name = name,
                    DisplayName = localizedName + " (" + guid + ")",
                    Description = localizedDescription,
                    Location = location
                };

                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in config)
                {
                    ConfigEntryBase entry = pair.Value;
                    if (entry == null)
                        continue;

                    ConfigEntryModel model = ConfigEntryModel.Create(
                        guid,
                        name,
                        pair.Key.Section,
                        pair.Key.Key,
                        entry);

                    ConfigSectionGroup section = plugin.Sections.FirstOrDefault(x => x.Name == model.Section);

                    if (section == null)
                    {
                        section = new ConfigSectionGroup
                        {
                            Name = model.Section,
                            DisplayName = SulfurLocalization.Get(
                                guid,
                                "section." + model.Section,
                                model.Section)
                        };

                        plugin.Sections.Add(section);
                    }

                    section.Entries.Add(model);
                }

                if (plugin.Sections.Count > 0)
                    result.Add(plugin);
            }

            foreach (ConfigPluginGroup plugin in result)
            {
                plugin.Sections = plugin.Sections
                    .OrderBy(x => x.DisplayName)
                    .ToList();

                foreach (ConfigSectionGroup section in plugin.Sections)
                {
                    section.Entries = section.Entries
                        .OrderBy(x => x.DisplayName)
                        .ToList();
                }
            }

            return result
                .OrderBy(x => x.DisplayName)
                .ToList();
        }
    }
}
