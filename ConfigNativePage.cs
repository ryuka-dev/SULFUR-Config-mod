using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using Ryuka.Sulfur.NativeUI;
using UnityEngine;

namespace Ryuka.SulfurConfig
{
    internal sealed class ConfigNativePage
    {
        private readonly ManualLogSource logger;

        private List<ConfigPluginGroup> groups = new List<ConfigPluginGroup>();

        // Stores unsaved edits across ctx.Rebuild() / ConfigScanner.Scan() calls.
        // Without this, a text/number change can be overwritten by a rebuild before Apply.
        private readonly Dictionary<string, string> draftCache = new Dictionary<string, string>();

        // Avoid rescanning every loaded BepInEx config on every UI rebuild.
        // ctx.Rebuild() is used to refresh badges, but scanning all mods every time is expensive.
        private bool needsScan = true;

        private string filterDraft = "";
        private string activeFilter = "";
        private string status = "";

        private bool showOnlyDirty;
        private bool showOnlyRyuka;
        private bool showAdvanced;
        private bool showHidden;
        private bool showRestartRequired;
        private bool showLiveApply;
        private bool showDangerous;

        public ConfigNativePage(ManualLogSource logger)
        {
            this.logger = logger;
        }

        public void Build(SulfurOptionsContext ctx)
        {
            EnsureScanned();

            int totalEntries = CountEntries(groups);
            int pendingEntries = CountDirty(groups);

            ctx.SetFooter(
                Plugin.L("badge.entries", "Entries") + ": " + totalEntries + " | " +
                Plugin.L("badge.dirty", "Pending") + ": " + pendingEntries,
                string.IsNullOrWhiteSpace(status)
                    ? Plugin.L("footer.ready", "Ready. Changes are not saved until you click Apply.")
                    : status,
                Plugin.L("button.apply_dirty", "Apply"),
                () => ApplyDirty(ctx));

            ctx.AddSection(Plugin.L("section.tools", "Tools"));

            ctx.AddSettingText(
                new SulfurSettingRow
                {
                    Label = Plugin.L("filter.label", "Filter"),
                    Description = Plugin.L("filter.description", "Search by mod name, GUID, section, key, or description."),
                    IsDirty = filterDraft != activeFilter,
                    ShowDefaultButton = false,
                    DirtyText = Plugin.L("badge.dirty", "Pending"),
                    CleanText = Plugin.L("badge.clean", "Unchanged")
                },
                filterDraft,
                value =>
                {
                    filterDraft = value ?? "";
                    ctx.SetFooterStatus(Plugin.L("status.filter_draft", "Filter draft changed."));
                });

            ctx.AddSmallButton(Plugin.L("button.apply_filter", "Apply Filter"), () =>
            {
                activeFilter = filterDraft ?? "";
                status = Plugin.L("status.filter_applied", "Filter applied.");
                ctx.Rebuild();
            });

            ctx.AddSmallButton(Plugin.L("button.clear_filter", "Clear Filter"), () =>
            {
                filterDraft = "";
                activeFilter = "";
                status = Plugin.L("status.filter_cleared", "Filter cleared.");
                ctx.Rebuild();
            });

            ctx.AddSmallButton(Plugin.L("button.refresh", "Refresh"), () =>
            {
                needsScan = true;
                status = Plugin.L("status.refreshing", "Refreshing config list.");
                ctx.Rebuild();
            });

            ctx.AddSmallButton(
                showOnlyDirty ? Plugin.L("filter.dirty_on", "Pending: ON") : Plugin.L("filter.dirty_off", "Pending: OFF"),
                () =>
                {
                    showOnlyDirty = !showOnlyDirty;
                    ctx.Rebuild();
                });

            ctx.AddSmallButton(
                showOnlyRyuka ? Plugin.L("filter.ryuka_on", "Ryuka: ON") : Plugin.L("filter.ryuka_off", "Ryuka: OFF"),
                () =>
                {
                    showOnlyRyuka = !showOnlyRyuka;
                    ctx.Rebuild();
                });

            ctx.AddSmallButton(
                showAdvanced ? Plugin.L("filter.advanced_on", "Advanced: ON") : Plugin.L("filter.advanced_off", "Advanced: OFF"),
                () =>
                {
                    showAdvanced = !showAdvanced;
                    ctx.Rebuild();
                });

            ctx.AddSmallButton(
                showHidden ? Plugin.L("filter.hidden_on", "Hidden: ON") : Plugin.L("filter.hidden_off", "Hidden: OFF"),
                () =>
                {
                    showHidden = !showHidden;
                    ctx.Rebuild();
                });

            ctx.AddSmallButton(
                showRestartRequired ? Plugin.L("filter.restart_on", "Restart: ON") : Plugin.L("filter.restart_off", "Restart: OFF"),
                () =>
                {
                    showRestartRequired = !showRestartRequired;
                    ctx.Rebuild();
                });

            ctx.AddSmallButton(
                showLiveApply ? Plugin.L("filter.live_on", "Live: ON") : Plugin.L("filter.live_off", "Live: OFF"),
                () =>
                {
                    showLiveApply = !showLiveApply;
                    ctx.Rebuild();
                });

            ctx.AddSmallButton(
                showDangerous ? Plugin.L("filter.dangerous_on", "Dangerous: ON") : Plugin.L("filter.dangerous_off", "Dangerous: OFF"),
                () =>
                {
                    showDangerous = !showDangerous;
                    ctx.Rebuild();
                });

            ctx.AddSpacer(12f);

            int visiblePlugins = 0;
            int visibleEntries = 0;

            foreach (ConfigPluginGroup plugin in groups)
            {
                List<ConfigSectionGroup> visibleSections = GetVisibleSections(plugin);

                if (visibleSections.Count == 0)
                    continue;

                int pluginEntryCount = visibleSections.Sum(s => s.Entries.Count(EntryMatches));
                int pluginPendingCount = visibleSections.Sum(s => s.Entries.Count(e => EntryMatches(e) && e.IsDirty));

                bool forceExpanded = HasFilterOrSpecialFilter();

                bool pluginExpanded = ctx.AddPluginFoldoutWithBadges(
                    "plugin." + plugin.Guid,
                    plugin.DisplayName,
                    false,
                    forceExpanded,
                    Plugin.L("badge.entries", "Entries") + ": " + pluginEntryCount,
                    pluginPendingCount > 0
                        ? Plugin.L("badge.dirty", "Pending") + ": " + pluginPendingCount
                        : Plugin.L("badge.clean", "Unchanged"));

                visiblePlugins++;

                if (!pluginExpanded)
                    continue;

                if (!string.IsNullOrWhiteSpace(plugin.Description))
                    ctx.AddDescription(plugin.Description);

                foreach (ConfigSectionGroup section in visibleSections)
                {
                    List<ConfigEntryModel> visibleSectionEntries = section.Entries
                        .Where(EntryMatches)
                        .ToList();

                    if (visibleSectionEntries.Count == 0)
                        continue;

                    int sectionPending = visibleSectionEntries.Count(x => x.IsDirty);

                    bool sectionExpanded = ctx.AddSectionFoldout(
                        "plugin." + plugin.Guid + ".section." + section.Name,
                        section.DisplayName,
                        false,
                        forceExpanded);

                    if (!sectionExpanded)
                        continue;

                    using (ctx.BeginThemedGroup("section.content." + plugin.Guid + "." + section.Name))
                    {
                        ctx.AddBadgeRow(
                            Plugin.L("badge.entries", "Entries") + ": " + visibleSectionEntries.Count,
                            sectionPending > 0
                                ? Plugin.L("badge.dirty", "Pending") + ": " + sectionPending
                                : Plugin.L("badge.clean", "Unchanged"));

                        foreach (ConfigEntryModel entry in visibleSectionEntries)
                        {
                            DrawEntry(ctx, entry);
                            visibleEntries++;
                        }
                    }
                }
            }

            if (visiblePlugins == 0)
            {
                ctx.AddWarning(Plugin.L("message.no_results", "No config entries match the current filters."));
            }
        }

        private void EnsureScanned()
        {
            if (!needsScan && groups != null && groups.Count > 0)
                return;

            groups = ConfigScanner.Scan(logger);
            RestoreDrafts(groups);
            needsScan = false;
        }

        private void DrawEntry(SulfurOptionsContext ctx, ConfigEntryModel entry)
        {
            SulfurSettingHandle handle = null;

            SulfurSettingRow row = new SulfurSettingRow
            {
                Label = entry.DisplayName,
                Description = entry.Description,
                IndentLevel = 1,
                IsDirty = entry.IsDirty,
                RequiresRestart = entry.RequiresRestart,
                LiveApply = entry.LiveApply,
                Advanced = entry.Advanced,
                Hidden = entry.Hidden,
                Dangerous = entry.Dangerous,

                Message = entry.IsDirty
                    ? Plugin.L("message.pending_warning", "This setting has unsaved pending changes. Click Apply to write it to cfg.")
                    : entry.RangeText,
                MessageKind = entry.IsDirty ? SulfurMessageKind.Warning : SulfurMessageKind.Info,

                DirtyText = Plugin.L("badge.dirty", "Pending"),
                CleanText = Plugin.L("badge.clean", "Unchanged"),
                RestartRequiredText = Plugin.L("badge.restart_required", "Restart Required"),
                LiveApplyText = Plugin.L("badge.live_apply", "Live Apply"),
                AdvancedText = Plugin.L("badge.advanced", "Advanced"),
                HiddenText = Plugin.L("badge.hidden", "Hidden"),
                DangerousText = Plugin.L("badge.dangerous", "Dangerous"),
                DefaultButtonText = Plugin.L("button.default", "Default"),
                ExtraBadges = entry.Badges.ToArray(),
                OnDefault = () =>
                {
                    entry.SetDefaultDraft();
                    SaveDraft(entry);
                    status = Plugin.L("status.default_draft", "Default draft set: ") + entry.Key;
                    UpdatePendingUi(ctx, entry, handle);
                }
            };

            Type type = entry.SettingType;

            if (type == typeof(bool))
            {
                ctx.AddSettingToggleEx(
                    row,
                    entry.GetBoolDraft(),
                    (value, h) =>
                    {
                        entry.SetDraft(value ? "true" : "false");
                        SaveDraft(entry);
                        status = Plugin.L("status.changed", "Pending change: ") + entry.Key;
                        UpdatePendingUi(ctx, entry, h);
                    },
                    out handle);

                return;
            }

            if (entry.ValueList.Count > 0)
            {
                List<string> displayValues = entry.ValueList
                    .Select(entry.GetLocalizedValueLabel)
                    .ToList();

                int index = entry.ValueList.IndexOf(entry.DraftValue);
                if (index < 0)
                    index = 0;

                ctx.AddSettingCycleEx(
                    row,
                    displayValues,
                    index,
                    (newIndex, selectedDisplayValue, h) =>
                    {
                        if (newIndex >= 0 && newIndex < entry.ValueList.Count)
                            entry.SetDraft(entry.ValueList[newIndex]);

                        SaveDraft(entry);
                        status = Plugin.L("status.changed", "Pending change: ") + entry.Key;
                        UpdatePendingUi(ctx, entry, h);
                    },
                    out handle);

                return;
            }

            if (type.IsEnum)
            {
                List<string> rawValues = Enum.GetNames(type).ToList();
                List<string> displayValues = rawValues
                    .Select(entry.GetLocalizedValueLabel)
                    .ToList();

                int index = rawValues.IndexOf(entry.DraftValue);
                if (index < 0)
                    index = 0;

                ctx.AddSettingCycleEx(
                    row,
                    displayValues,
                    index,
                    (newIndex, selectedDisplayValue, h) =>
                    {
                        if (newIndex >= 0 && newIndex < rawValues.Count)
                            entry.SetDraft(rawValues[newIndex]);

                        SaveDraft(entry);
                        status = Plugin.L("status.changed", "Pending change: ") + entry.Key;
                        UpdatePendingUi(ctx, entry, h);
                    },
                    out handle);

                return;
            }

            if (type == typeof(int) || type == typeof(float) || type == typeof(double))
            {
                float current = entry.GetFloatDraft();
                float min = entry.MinValue.HasValue ? entry.MinValue.Value : entry.GetDefaultNumericMin();
                float max = entry.MaxValue.HasValue ? entry.MaxValue.Value : entry.GetDefaultNumericMax();
                int decimals = type == typeof(int) ? 0 : 3;

                ctx.AddSettingNumberEx(
                    row,
                    current,
                    min,
                    max,
                    decimals,
                    (value, h) =>
                    {
                        if (type == typeof(int))
                            entry.SetDraft(Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture));
                        else
                            entry.SetDraft(value.ToString("0.###", CultureInfo.InvariantCulture));

                        SaveDraft(entry);
                        status = Plugin.L("status.changed", "Pending change: ") + entry.Key;
                        UpdatePendingUi(ctx, entry, h);
                    },
                    out handle);

                return;
            }

            ctx.AddSettingTextEx(
                row,
                entry.DraftValue,
                (value, h) =>
                {
                    entry.SetDraft(value);
                    SaveDraft(entry);
                    status = Plugin.L("status.changed", "Pending change: ") + entry.Key;
                    UpdatePendingUi(ctx, entry, h);
                },
                out handle);
        }

        private void UpdatePendingUi(SulfurOptionsContext ctx, ConfigEntryModel entry, SulfurSettingHandle handle)
        {
            if (handle != null)
            {
                handle.SetDirty(
                    entry.IsDirty,
                    Plugin.L("badge.dirty", "Pending"),
                    Plugin.L("badge.clean", "Unchanged"));

                handle.SetMessage(
                    entry.IsDirty
                        ? Plugin.L("message.pending_warning", "This setting has unsaved pending changes. Click Apply to write it to cfg.")
                        : entry.RangeText,
                    entry.IsDirty ? SulfurMessageKind.Warning : SulfurMessageKind.Info);
            }

            UpdateFooter(ctx);
        }

        private void UpdateFooter(SulfurOptionsContext ctx)
        {
            if (ctx == null)
                return;

            ctx.SetFooter(
                Plugin.L("badge.entries", "Entries") + ": " + CountEntries(groups) + " | " +
                Plugin.L("badge.dirty", "Pending") + ": " + CountDirty(groups),
                string.IsNullOrWhiteSpace(status)
                    ? Plugin.L("footer.ready", "Ready. Changes are not saved until you click Apply.")
                    : status,
                Plugin.L("button.apply_dirty", "Apply"),
                () => ApplyDirty(ctx));
        }

        private void ApplyDirty(SulfurOptionsContext ctx)
        {
            int applied = 0;
            int failed = 0;

            foreach (ConfigPluginGroup plugin in groups)
            {
                foreach (ConfigSectionGroup section in plugin.Sections)
                {
                    foreach (ConfigEntryModel entry in section.Entries)
                    {
                        if (!entry.IsDirty)
                            continue;

                        try
                        {
                            entry.Apply();
                            ClearDraft(entry);
                            applied++;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            logger.LogWarning(
                                "Failed to apply " +
                                entry.PluginGuid + " / " +
                                entry.Section + " / " +
                                entry.Key + ": " +
                                ex.Message);
                        }
                    }
                }
            }

            status = Plugin.L("status.apply_result", "Applied and saved: ") + applied + ", " +
                     Plugin.L("status.failed", "Failed: ") + failed + ".";

            ctx.Rebuild();
        }

        private List<ConfigSectionGroup> GetVisibleSections(ConfigPluginGroup plugin)
        {
            List<ConfigSectionGroup> result = new List<ConfigSectionGroup>();

            if (plugin == null)
                return result;

            if (showOnlyRyuka && !Contains(plugin.Guid, "ryuka") && !Contains(plugin.Name, "ryuka"))
                return result;

            bool pluginTextHit = MatchesFilter(plugin.DisplayName) ||
                                 MatchesFilter(plugin.Guid) ||
                                 MatchesFilter(plugin.Name) ||
                                 MatchesFilter(plugin.Description);

            foreach (ConfigSectionGroup section in plugin.Sections)
            {
                bool sectionTextHit = MatchesFilter(section.DisplayName) || MatchesFilter(section.Name);

                bool hasAnyEntry = section.Entries.Any(entry =>
                    EntryPassesSpecialFilters(entry) &&
                    (NoTextFilter() ||
                     pluginTextHit ||
                     sectionTextHit ||
                     EntryTextMatches(entry)));

                if (hasAnyEntry)
                    result.Add(section);
            }

            return result;
        }

        private bool SectionMatches(ConfigSectionGroup section)
        {
            if (section == null)
                return false;

            return section.Entries.Any(EntryMatches);
        }

        private bool EntryMatches(ConfigEntryModel entry)
        {
            if (entry == null)
                return false;

            if (!EntryPassesSpecialFilters(entry))
                return false;

            if (NoTextFilter())
                return true;

            return EntryTextMatches(entry);
        }

        private bool EntryPassesSpecialFilters(ConfigEntryModel entry)
        {
            if (entry == null)
                return false;

            if (showOnlyDirty && !entry.IsDirty)
                return false;

            if (showAdvanced)
            {
                if (!entry.Advanced)
                    return false;
            }
            else if (entry.Advanced)
            {
                return false;
            }

            if (showHidden)
            {
                if (!entry.Hidden)
                    return false;
            }
            else if (entry.Hidden)
            {
                return false;
            }

            if (showRestartRequired && !entry.RequiresRestart)
                return false;

            if (showLiveApply && !entry.LiveApply)
                return false;

            if (showDangerous && !entry.Dangerous)
                return false;

            return true;
        }

        private bool EntryTextMatches(ConfigEntryModel entry)
        {
            return MatchesFilter(entry.PluginName) ||
                   MatchesFilter(entry.PluginGuid) ||
                   MatchesFilter(entry.Section) ||
                   MatchesFilter(entry.Key) ||
                   MatchesFilter(entry.DisplayName) ||
                   MatchesFilter(entry.Description);
        }

        private bool MatchesFilter(string value)
        {
            if (NoTextFilter())
                return true;

            return Contains(value, activeFilter);
        }

        private bool NoTextFilter()
        {
            return string.IsNullOrWhiteSpace(activeFilter);
        }

        private bool HasFilterOrSpecialFilter()
        {
            return !NoTextFilter() ||
                   showOnlyDirty ||
                   showOnlyRyuka ||
                   showAdvanced ||
                   showHidden ||
                   showRestartRequired ||
                   showLiveApply ||
                   showDangerous;
        }

        private void SaveDraft(ConfigEntryModel entry)
        {
            if (entry == null)
                return;

            string key = GetDraftKey(entry);

            if (entry.IsDirty)
                draftCache[key] = entry.DraftValue;
            else
                draftCache.Remove(key);
        }

        private void ClearDraft(ConfigEntryModel entry)
        {
            if (entry == null)
                return;

            draftCache.Remove(GetDraftKey(entry));
        }

        private void RestoreDrafts(List<ConfigPluginGroup> source)
        {
            if (source == null || draftCache.Count == 0)
                return;

            foreach (ConfigPluginGroup plugin in source)
            {
                foreach (ConfigSectionGroup section in plugin.Sections)
                {
                    foreach (ConfigEntryModel entry in section.Entries)
                    {
                        string cached;
                        if (draftCache.TryGetValue(GetDraftKey(entry), out cached))
                            entry.SetDraft(cached);
                    }
                }
            }
        }

        private static string GetDraftKey(ConfigEntryModel entry)
        {
            return entry.PluginGuid + "\u001f" + entry.Section + "\u001f" + entry.Key;
        }

        private static bool Contains(string text, string filter)
        {
            return !string.IsNullOrEmpty(text) &&
                   !string.IsNullOrEmpty(filter) &&
                   text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountEntries(List<ConfigPluginGroup> source)
        {
            if (source == null)
                return 0;

            return source.Sum(CountEntries);
        }

        private static int CountDirty(List<ConfigPluginGroup> source)
        {
            if (source == null)
                return 0;

            return source.Sum(CountDirty);
        }

        private static int CountEntries(ConfigPluginGroup plugin)
        {
            if (plugin == null || plugin.Sections == null)
                return 0;

            return plugin.Sections.Sum(s => s.Entries.Count);
        }

        private static int CountDirty(ConfigPluginGroup plugin)
        {
            if (plugin == null || plugin.Sections == null)
                return 0;

            return plugin.Sections.Sum(s => s.Entries.Count(e => e.IsDirty));
        }
    }
}
