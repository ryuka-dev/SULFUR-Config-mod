using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ryuka.SulfurConfig
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.config";
        public const string PluginName = "SULFUR Config";
        public const string PluginVersion = "0.3.0";

        private SulfurConfigWindow window;

        private ConfigEntry<string> openKey;
        private ConfigEntry<bool> showSelfConfig;
        private ConfigEntry<bool> onlyRyukaModsDefault;
        private ConfigEntry<bool> confirmApplyAll;
        private ConfigEntry<bool> confirmDangerousApply;

        private ConfigEntry<bool> lockGameInput;
        private ConfigEntry<string> lockedActionMaps;
        private ConfigEntry<string> ownModFilterKeywords;

        private ConfigEntry<bool> createBackupBeforeSave;
        private ConfigEntry<string> backupDirectoryName;

        private ConfigEntry<float> windowWidth;
        private ConfigEntry<float> windowHeight;
        private ConfigEntry<float> fontScale;

        private Key cachedOpenKey = Key.F10;
        private string cachedOpenKeyText = "F10";

        private void Awake()
        {
            BindConfig();
            RebuildCachedOpenKey();

            window = new SulfurConfigWindow(
                Logger,
                new SulfurConfigSettings
                {
                    ShowSelfConfig = showSelfConfig,
                    OnlyRyukaModsDefault = onlyRyukaModsDefault,
                    ConfirmApplyAll = confirmApplyAll,
                    ConfirmDangerousApply = confirmDangerousApply,
                    LockGameInput = lockGameInput,
                    LockedActionMaps = lockedActionMaps,
                    OwnModFilterKeywords = ownModFilterKeywords,
                    CreateBackupBeforeSave = createBackupBeforeSave,
                    BackupDirectoryName = backupDirectoryName,
                    WindowWidth = windowWidth,
                    WindowHeight = windowHeight,
                    FontScale = fontScale
                },
                new GameInputBlocker(Logger));

            Config.SettingChanged += OnOwnConfigChanged;

            Logger.LogInfo("SULFUR Config loaded. Press " + cachedOpenKeyText + " to open the config panel.");
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnOwnConfigChanged;

            if (window != null)
                window.ForceClose();
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            try
            {
                KeyControl keyControl = Keyboard.current[cachedOpenKey];
                if (keyControl != null && keyControl.wasPressedThisFrame)
                    window.Toggle();
            }
            catch
            {
                // Ignore invalid input state.
            }
        }

        private void OnGUI()
        {
            if (window != null)
                window.OnGUI();
        }

        private void BindConfig()
        {
            openKey = Config.Bind(
                "General",
                "OpenKey",
                "F10",
                new ConfigDescription(
                    "Keyboard key used to open or close SULFUR Config. Use Unity InputSystem key names such as F10, F9, Insert, Home."
                )
            );

            showSelfConfig = Config.Bind(
                "General",
                "ShowSelfConfig",
                false,
                new ConfigDescription(
                    "Show SULFUR Config's own settings in the config list."
                )
            );

            onlyRyukaModsDefault = Config.Bind(
                "General",
                "OnlyRyukaModsDefault",
                false,
                new ConfigDescription(
                    "Enable the 'Only Ryuka Mods' filter by default."
                )
            );

            confirmApplyAll = Config.Bind(
                "Safety",
                "ConfirmApplyAll",
                true,
                new ConfigDescription(
                    "Require a second click before applying all dirty settings."
                )
            );

            confirmDangerousApply = Config.Bind(
                "Safety",
                "ConfirmDangerousApply",
                true,
                new ConfigDescription(
                    "Require confirmation before applying settings tagged as dangerous."
                )
            );

            createBackupBeforeSave = Config.Bind(
                "Safety",
                "CreateBackupBeforeSave",
                true,
                new ConfigDescription(
                    "Create a backup of the target cfg file before applying or resetting settings."
                )
            );

            backupDirectoryName = Config.Bind(
                "Safety",
                "BackupDirectoryName",
                "SULFURConfigBackups",
                new ConfigDescription(
                    "Backup folder name inside BepInEx/config."
                )
            );

            lockGameInput = Config.Bind(
                "Input",
                "LockGameInputWhenOpen",
                true,
                new ConfigDescription(
                    "Disable SULFUR gameplay input while the SULFUR Config window is open."
                )
            );

            lockedActionMaps = Config.Bind(
                "Input",
                "LockedActionMaps",
                "OnFoot,Inventory,FKeys,UI,DevTools",
                new ConfigDescription(
                    "PlayerInputActions maps to disable while the SULFUR Config window is open. Default blocks movement, camera, hotkeys, inventory, UI, and dev controls."
                )
            );

            ownModFilterKeywords = Config.Bind(
                "Filter",
                "OwnModFilterKeywords",
                "ryuka",
                new ConfigDescription(
                    "Keywords used by the 'Only Ryuka Mods' filter. It checks plugin name, GUID, DLL path, and cfg path. Separate with comma."
                )
            );

            windowWidth = Config.Bind(
                "UI",
                "WindowWidth",
                1120f,
                new ConfigDescription(
                    "Default SULFUR Config window width.",
                    new AcceptableValueRange<float>(700f, 2400f)
                )
            );

            windowHeight = Config.Bind(
                "UI",
                "WindowHeight",
                740f,
                new ConfigDescription(
                    "Default SULFUR Config window height.",
                    new AcceptableValueRange<float>(480f, 1600f)
                )
            );

            fontScale = Config.Bind(
                "UI",
                "FontScale",
                1f,
                new ConfigDescription(
                    "IMGUI font scale. 1.0 is normal.",
                    new AcceptableValueRange<float>(0.75f, 1.75f)
                )
            );
        }

        private void OnOwnConfigChanged(object sender, SettingChangedEventArgs args)
        {
            if (args == null || args.ChangedSetting == null)
                return;

            if (args.ChangedSetting == openKey)
            {
                RebuildCachedOpenKey();
                Logger.LogInfo("SULFUR Config open key changed to: " + cachedOpenKeyText);
            }

            if (window != null)
                window.MarkLayoutDirty();
        }

        private void RebuildCachedOpenKey()
        {
            string value = openKey != null ? openKey.Value : "F10";
            Key parsed;

            if (TryParseKey(value, out parsed))
            {
                cachedOpenKey = parsed;
                cachedOpenKeyText = parsed.ToString();
                return;
            }

            cachedOpenKey = Key.F10;
            cachedOpenKeyText = "F10";
            Logger.LogWarning("Invalid OpenKey '" + value + "'. Falling back to F10.");
        }

        private static bool TryParseKey(string text, out Key key)
        {
            key = Key.F10;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.Trim();

            if (Enum.TryParse(cleaned, true, out key))
                return true;

            cleaned = cleaned.Replace(" ", "");
            return Enum.TryParse(cleaned, true, out key);
        }
    }

    internal sealed class SulfurConfigSettings
    {
        public ConfigEntry<bool> ShowSelfConfig;
        public ConfigEntry<bool> OnlyRyukaModsDefault;
        public ConfigEntry<bool> ConfirmApplyAll;
        public ConfigEntry<bool> ConfirmDangerousApply;

        public ConfigEntry<bool> LockGameInput;
        public ConfigEntry<string> LockedActionMaps;
        public ConfigEntry<string> OwnModFilterKeywords;

        public ConfigEntry<bool> CreateBackupBeforeSave;
        public ConfigEntry<string> BackupDirectoryName;

        public ConfigEntry<float> WindowWidth;
        public ConfigEntry<float> WindowHeight;
        public ConfigEntry<float> FontScale;
    }

    internal sealed class SulfurConfigWindow
    {
        private readonly ManualLogSource logger;
        private readonly SulfurConfigSettings settings;
        private readonly GameInputBlocker inputBlocker;

        private readonly List<ConfigEntryModel> allEntries = new List<ConfigEntryModel>();
        private readonly List<string> pluginKeys = new List<string>();

        private bool visible;
        private bool initialized;
        private bool layoutDirty = true;

        private Rect windowRect;
        private Vector2 pluginScroll;
        private Vector2 entryScroll;

        private string selectedPluginKey;
        private string searchText = "";
        private string statusText = "Ready.";

        private bool showOnlyDirty;
        private bool onlyRyukaMods;
        private bool advancedOnly;
        private bool hiddenOnly;
        private bool restartRequiredOnly;
        private bool liveApplyOnly;
        private bool dangerousOnly;

        private bool pendingApplyAllConfirm;
        private int pendingDangerousModelId = -1;

        private CursorLockMode oldCursorLockMode;
        private bool oldCursorVisible;

        private int nextModelId = 1;

        public SulfurConfigWindow(ManualLogSource logger, SulfurConfigSettings settings, GameInputBlocker inputBlocker)
        {
            this.logger = logger;
            this.settings = settings;
            this.inputBlocker = inputBlocker;
            this.onlyRyukaMods = settings.OnlyRyukaModsDefault.Value;
        }

        public void Toggle()
        {
            if (visible)
            {
                ForceClose();
                return;
            }

            visible = true;

            oldCursorLockMode = Cursor.lockState;
            oldCursorVisible = Cursor.visible;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (settings.LockGameInput.Value)
                inputBlocker.Lock(settings.LockedActionMaps.Value);

            Scan();
        }

        public void ForceClose()
        {
            if (!visible)
                return;

            visible = false;

            inputBlocker.Unlock();

            Cursor.lockState = oldCursorLockMode;
            Cursor.visible = oldCursorVisible;

            pendingApplyAllConfirm = false;
            pendingDangerousModelId = -1;
        }

        public void MarkLayoutDirty()
        {
            layoutDirty = true;
        }

        public void OnGUI()
        {
            if (!visible)
                return;

            ApplyFontScale();

            if (!initialized || layoutDirty)
            {
                float width = Mathf.Clamp(settings.WindowWidth.Value, 700f, Mathf.Max(700f, Screen.width - 40f));
                float height = Mathf.Clamp(settings.WindowHeight.Value, 480f, Mathf.Max(480f, Screen.height - 40f));

                if (!initialized)
                {
                    windowRect = new Rect(30f, 30f, width, height);
                    initialized = true;
                }
                else
                {
                    windowRect.width = width;
                    windowRect.height = height;
                }

                layoutDirty = false;
            }

            GUI.depth = -1000;
            windowRect = GUI.Window(824603, windowRect, DrawWindow, "SULFUR Config v0.3");
        }

        private void ApplyFontScale()
        {
            float scale = Mathf.Clamp(settings.FontScale.Value, 0.75f, 1.75f);
            int size = Mathf.RoundToInt(13f * scale);

            GUI.skin.label.fontSize = size;
            GUI.skin.button.fontSize = size;
            GUI.skin.textField.fontSize = size;
            GUI.skin.toggle.fontSize = size;
            GUI.skin.window.fontSize = Mathf.RoundToInt(14f * scale);
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            DrawToolbar();

            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();

            DrawPluginList();

            GUILayout.Space(8f);

            DrawEntryList();

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            DrawFooter();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Refresh", GUILayout.Width(84f)))
            {
                Scan();
                statusText = "Refreshed loaded config entries.";
            }

            if (GUILayout.Button("Reload Disk", GUILayout.Width(100f)))
            {
                ReloadAllFromDisk();
            }

            if (GUILayout.Button(pendingApplyAllConfirm ? "Confirm Apply" : "Apply Dirty", GUILayout.Width(120f)))
            {
                OnApplyAllPressed();
            }

            if (GUILayout.Button("Revert Dirty", GUILayout.Width(104f)))
            {
                RevertAllDirty();
            }

            if (GUILayout.Button("Close", GUILayout.Width(70f)))
            {
                ForceClose();
            }

            GUILayout.Space(10f);

            GUILayout.Label("Search", GUILayout.Width(48f));
            string newSearch = GUILayout.TextField(searchText ?? "", GUILayout.MinWidth(180f));

            if (newSearch != searchText)
            {
                searchText = newSearch;
                entryScroll = Vector2.zero;
                pendingApplyAllConfirm = false;
                RebuildPluginKeysKeepSelection();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            bool newDirty = GUILayout.Toggle(showOnlyDirty, "Dirty Only", GUILayout.Width(92f));
            bool newRyuka = GUILayout.Toggle(onlyRyukaMods, "Only Ryuka Mods", GUILayout.Width(130f));
            bool newAdvanced = GUILayout.Toggle(advancedOnly, "Advanced Only", GUILayout.Width(118f));
            bool newHidden = GUILayout.Toggle(hiddenOnly, "Hidden Only", GUILayout.Width(104f));
            bool newRestart = GUILayout.Toggle(restartRequiredOnly, "Restart Required", GUILayout.Width(132f));
            bool newLive = GUILayout.Toggle(liveApplyOnly, "Live Apply", GUILayout.Width(100f));
            bool newDangerous = GUILayout.Toggle(dangerousOnly, "Dangerous", GUILayout.Width(100f));

            if (newDirty != showOnlyDirty ||
                newRyuka != onlyRyukaMods ||
                newAdvanced != advancedOnly ||
                newHidden != hiddenOnly ||
                newRestart != restartRequiredOnly ||
                newLive != liveApplyOnly ||
                newDangerous != dangerousOnly)
            {
                showOnlyDirty = newDirty;
                onlyRyukaMods = newRyuka;
                advancedOnly = newAdvanced;
                hiddenOnly = newHidden;
                restartRequiredOnly = newRestart;
                liveApplyOnly = newLive;
                dangerousOnly = newDangerous;

                entryScroll = Vector2.zero;
                pendingApplyAllConfirm = false;
                pendingDangerousModelId = -1;
                RebuildPluginKeysKeepSelection();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPluginList()
        {
            GUILayout.BeginVertical(GUILayout.Width(290f));

            GUILayout.Label("Mods", GUILayout.Height(22f));

            pluginScroll = GUILayout.BeginScrollView(pluginScroll, GUI.skin.box);

            foreach (string pluginKey in pluginKeys)
            {
                int dirtyCount = allEntries.Count(x => x.PluginKey == pluginKey && x.IsDirty);
                string label = pluginKey;

                if (dirtyCount > 0)
                    label += "  *" + dirtyCount;

                GUIStyle style = pluginKey == selectedPluginKey ? GUI.skin.button : GUI.skin.label;

                if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
                {
                    selectedPluginKey = pluginKey;
                    entryScroll = Vector2.zero;
                    pendingApplyAllConfirm = false;
                    pendingDangerousModelId = -1;
                }
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawEntryList()
        {
            GUILayout.BeginVertical();

            string title = string.IsNullOrEmpty(selectedPluginKey) ? "No mod selected" : selectedPluginKey;
            GUILayout.Label(title, GUILayout.Height(22f));

            entryScroll = GUILayout.BeginScrollView(entryScroll, GUI.skin.box);

            List<ConfigEntryModel> entries = GetVisibleEntriesForSelectedPlugin();

            if (entries.Count == 0)
            {
                GUILayout.Label("No config entries match the current filters.");
                GUILayout.Label("Note: Restart Required / Live Apply / Advanced / Hidden only work when target config entries use matching tags.");
            }

            string lastSection = null;

            foreach (ConfigEntryModel model in entries)
            {
                if (model.Section != lastSection)
                {
                    GUILayout.Space(8f);
                    GUILayout.Label("[" + model.Section + "]");
                    lastSection = model.Section;
                }

                DrawEntryRow(model);
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawEntryRow(ConfigEntryModel model)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();

            GUILayout.Label(model.Key, GUILayout.Width(260f));
            GUILayout.Label(model.IsDirty ? "*" : "", GUILayout.Width(14f));

            DrawValueControl(model);

            string applyText = pendingDangerousModelId == model.Id ? "Confirm" : "Apply";
            if (GUILayout.Button(applyText, GUILayout.Width(76f)))
            {
                OnApplyOnePressed(model);
            }

            if (GUILayout.Button("Revert", GUILayout.Width(70f)))
            {
                model.RevertEdit();
                model.ErrorText = null;
                pendingDangerousModelId = -1;
                statusText = "Reverted edit: " + model.Key;
            }

            if (GUILayout.Button("Default", GUILayout.Width(76f)))
            {
                ResetOneToDefault(model);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Current: " + SafeToString(model.CurrentSerializedValue), GUILayout.Width(260f));
            GUILayout.Label("Default: " + SafeToString(model.DefaultSerializedValue), GUILayout.Width(260f));
            GUILayout.Label(model.RuntimeStatusLabel, GUILayout.Width(170f));

            if (model.Advanced)
                GUILayout.Label("Advanced", GUILayout.Width(86f));

            if (model.Hidden)
                GUILayout.Label("Hidden", GUILayout.Width(72f));

            if (model.Dangerous)
                GUILayout.Label("Dangerous", GUILayout.Width(90f));

            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(model.Description))
                GUILayout.Label(model.Description);

            if (!string.IsNullOrWhiteSpace(model.RangeText))
                GUILayout.Label(model.RangeText);

            if (!string.IsNullOrWhiteSpace(model.ErrorText))
                GUILayout.Label("Error: " + model.ErrorText);

            GUILayout.EndVertical();
        }

        private void DrawValueControl(ConfigEntryModel model)
        {
            Type type = model.SettingType;

            if (type == typeof(bool))
            {
                bool value = ParseBool(model.EditedSerializedValue);
                bool newValue = GUILayout.Toggle(value, value ? "true" : "false", GUILayout.Width(120f));
                model.EditedSerializedValue = newValue ? "true" : "false";
                return;
            }

            if (model.HasValueList)
            {
                DrawValueListControl(model);
                return;
            }

            if (type.IsEnum)
            {
                DrawEnumControl(model);
                return;
            }

            if (model.HasRange && IsNumericType(type))
            {
                DrawRangeControl(model);
                return;
            }

            model.EditedSerializedValue = GUILayout.TextField(model.EditedSerializedValue ?? "", GUILayout.MinWidth(260f));
        }

        private void DrawValueListControl(ConfigEntryModel model)
        {
            string[] values = model.AcceptableValueStrings;

            if (values == null || values.Length == 0)
            {
                model.EditedSerializedValue = GUILayout.TextField(model.EditedSerializedValue ?? "", GUILayout.MinWidth(260f));
                return;
            }

            int index = Array.IndexOf(values, model.EditedSerializedValue);
            if (index < 0)
                index = 0;

            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                index--;
                if (index < 0)
                    index = values.Length - 1;

                model.EditedSerializedValue = values[index];
            }

            GUILayout.Label(values[index], GUILayout.Width(190f));

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                index++;
                if (index >= values.Length)
                    index = 0;

                model.EditedSerializedValue = values[index];
            }
        }

        private void DrawEnumControl(ConfigEntryModel model)
        {
            string[] names = Enum.GetNames(model.SettingType);

            if (names.Length == 0)
            {
                model.EditedSerializedValue = GUILayout.TextField(model.EditedSerializedValue ?? "", GUILayout.MinWidth(260f));
                return;
            }

            int index = Array.IndexOf(names, model.EditedSerializedValue);
            if (index < 0)
                index = 0;

            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                index--;
                if (index < 0)
                    index = names.Length - 1;

                model.EditedSerializedValue = names[index];
            }

            GUILayout.Label(names[index], GUILayout.Width(190f));

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                index++;
                if (index >= names.Length)
                    index = 0;

                model.EditedSerializedValue = names[index];
            }
        }

        private void DrawRangeControl(ConfigEntryModel model)
        {
            float min = Convert.ToSingle(model.MinValue, CultureInfo.InvariantCulture);
            float max = Convert.ToSingle(model.MaxValue, CultureInfo.InvariantCulture);

            float value;
            if (!float.TryParse(model.EditedSerializedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                float.TryParse(model.CurrentSerializedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            value = Mathf.Clamp(value, min, max);

            float newValue = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180f));

            if (model.SettingType == typeof(int))
            {
                int rounded = Mathf.RoundToInt(newValue);
                model.EditedSerializedValue = rounded.ToString(CultureInfo.InvariantCulture);
                model.EditedSerializedValue = GUILayout.TextField(model.EditedSerializedValue, GUILayout.Width(96f));
            }
            else
            {
                model.EditedSerializedValue = newValue.ToString("0.###", CultureInfo.InvariantCulture);
                model.EditedSerializedValue = GUILayout.TextField(model.EditedSerializedValue, GUILayout.Width(96f));
            }
        }

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            int total = allEntries.Count;
            int visibleCount = GetVisibleEntriesForSelectedPlugin().Count;
            int dirty = allEntries.Count(x => x.IsDirty);

            GUILayout.Label("Entries: " + total + "   Visible: " + visibleCount + "   Dirty: " + dirty, GUILayout.Width(300f));
            GUILayout.Label(statusText ?? "");

            GUILayout.EndHorizontal();
        }

        private void Scan()
        {
            allEntries.Clear();
            pluginKeys.Clear();
            nextModelId = 1;
            pendingApplyAllConfirm = false;
            pendingDangerousModelId = -1;

            try
            {
                foreach (PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
                {
                    if (pluginInfo == null || pluginInfo.Instance == null || pluginInfo.Metadata == null)
                        continue;

                    ConfigFile configFile = pluginInfo.Instance.Config;
                    if (configFile == null)
                        continue;

                    string pluginName = pluginInfo.Metadata.Name ?? pluginInfo.Metadata.GUID;
                    string pluginGuid = pluginInfo.Metadata.GUID ?? "unknown.guid";
                    string pluginVersion = pluginInfo.Metadata.Version != null ? pluginInfo.Metadata.Version.ToString() : "unknown";
                    string pluginLocation = pluginInfo.Location ?? "";
                    string pluginKey = pluginName + " (" + pluginGuid + ")";

                    if (!settings.ShowSelfConfig.Value && string.Equals(pluginGuid, Plugin.PluginGuid, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in configFile)
                    {
                        ConfigEntryBase entry = pair.Value;
                        if (entry == null)
                            continue;

                        ConfigEntryModel model = ConfigEntryModel.Create(
                            nextModelId++,
                            pluginName,
                            pluginGuid,
                            pluginVersion,
                            pluginKey,
                            pluginLocation,
                            configFile.ConfigFilePath,
                            entry);

                        allEntries.Add(model);
                    }
                }

                allEntries.Sort(CompareEntry);
                RebuildPluginKeysKeepSelection();

                statusText = "Loaded " + allEntries.Count + " config entries from " + pluginKeys.Count + " visible mods.";
            }
            catch (Exception ex)
            {
                statusText = "Scan failed: " + ex.Message;
                logger.LogError(ex);
            }
        }

        private void ReloadAllFromDisk()
        {
            try
            {
                List<ConfigFile> files = allEntries
                    .Select(x => x.Entry.ConfigFile)
                    .Where(x => x != null)
                    .Distinct()
                    .ToList();

                foreach (ConfigFile file in files)
                    file.Reload();

                Scan();

                statusText = "Reloaded " + files.Count + " cfg files from disk.";
            }
            catch (Exception ex)
            {
                statusText = "Reload from disk failed: " + ex.Message;
                logger.LogError(ex);
            }
        }

        private void RebuildPluginKeysKeepSelection()
        {
            string oldSelected = selectedPluginKey;

            pluginKeys.Clear();

            foreach (string pluginKey in GetFilteredEntriesWithoutSelectedPlugin().Select(x => x.PluginKey).Distinct())
                pluginKeys.Add(pluginKey);

            if (!string.IsNullOrEmpty(oldSelected) && pluginKeys.Contains(oldSelected))
                selectedPluginKey = oldSelected;
            else
                selectedPluginKey = pluginKeys.Count > 0 ? pluginKeys[0] : null;
        }

        private List<ConfigEntryModel> GetVisibleEntriesForSelectedPlugin()
        {
            IEnumerable<ConfigEntryModel> query = GetFilteredEntriesWithoutSelectedPlugin();

            if (!string.IsNullOrEmpty(selectedPluginKey))
                query = query.Where(x => x.PluginKey == selectedPluginKey);

            return query.ToList();
        }

        private IEnumerable<ConfigEntryModel> GetFilteredEntriesWithoutSelectedPlugin()
        {
            IEnumerable<ConfigEntryModel> query = allEntries;

            if (onlyRyukaMods)
                query = query.Where(IsOwnPlugin);

            if (showOnlyDirty)
                query = query.Where(x => x.IsDirty);

            if (advancedOnly)
                query = query.Where(x => x.Advanced);
            else
                query = query.Where(x => !x.Advanced);

            if (hiddenOnly)
                query = query.Where(x => x.Hidden);
            else
                query = query.Where(x => !x.Hidden);

            if (restartRequiredOnly)
                query = query.Where(x => x.RequiresRestart);

            if (liveApplyOnly)
                query = query.Where(x => x.LiveApply);

            if (dangerousOnly)
                query = query.Where(x => x.Dangerous);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                string s = searchText.Trim();

                query = query.Where(x =>
                    ContainsIgnoreCase(x.PluginName, s) ||
                    ContainsIgnoreCase(x.PluginGuid, s) ||
                    ContainsIgnoreCase(x.PluginLocation, s) ||
                    ContainsIgnoreCase(x.ConfigFilePath, s) ||
                    ContainsIgnoreCase(x.Section, s) ||
                    ContainsIgnoreCase(x.Key, s) ||
                    ContainsIgnoreCase(x.Description, s));
            }

            return query;
        }

        private bool IsOwnPlugin(ConfigEntryModel model)
        {
            if (model == null)
                return false;

            string keywordsText = settings.OwnModFilterKeywords.Value;
            if (string.IsNullOrWhiteSpace(keywordsText))
                return false;

            string[] keywords = keywordsText
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();

            foreach (string keyword in keywords)
            {
                if (ContainsIgnoreCase(model.PluginName, keyword) ||
                    ContainsIgnoreCase(model.PluginGuid, keyword) ||
                    ContainsIgnoreCase(model.PluginLocation, keyword) ||
                    ContainsIgnoreCase(model.ConfigFilePath, keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnApplyOnePressed(ConfigEntryModel model)
        {
            if (!model.IsDirty)
            {
                statusText = "No change: " + model.Key;
                return;
            }

            if (model.Dangerous && settings.ConfirmDangerousApply.Value && pendingDangerousModelId != model.Id)
            {
                pendingDangerousModelId = model.Id;
                statusText = "Dangerous setting. Click Confirm to apply: " + model.Key;
                return;
            }

            pendingDangerousModelId = -1;
            ApplyOne(model);
        }

        private void OnApplyAllPressed()
        {
            List<ConfigEntryModel> dirtyModels = allEntries.Where(x => x.IsDirty).ToList();

            if (dirtyModels.Count == 0)
            {
                statusText = "No dirty entries.";
                pendingApplyAllConfirm = false;
                return;
            }

            bool hasDangerous = dirtyModels.Any(x => x.Dangerous);

            if ((settings.ConfirmApplyAll.Value || hasDangerous) && !pendingApplyAllConfirm)
            {
                pendingApplyAllConfirm = true;
                statusText = "Click Confirm Apply to apply " + dirtyModels.Count + " dirty entries" +
                             (hasDangerous ? " including dangerous settings." : ".");
                return;
            }

            pendingApplyAllConfirm = false;
            ApplyAllDirty(dirtyModels);
        }

        private void ApplyOne(ConfigEntryModel model)
        {
            try
            {
                model.ErrorText = null;

                string requested = model.EditedSerializedValue;
                string before = model.Entry.GetSerializedValue();

                if (settings.CreateBackupBeforeSave.Value)
                    ConfigBackupUtility.TryBackup(model.Entry.ConfigFile, settings.BackupDirectoryName.Value, logger);

                model.Entry.SetSerializedValue(requested);
                model.Entry.ConfigFile.Save();
                model.RefreshFromEntry();

                string after = model.Entry.GetSerializedValue();

                if (before == after && before != requested)
                    statusText = "Apply may have been rejected: " + model.Key;
                else if (after != requested)
                    statusText = "Applied with adjusted value: " + model.Key + " => " + after;
                else
                    statusText = "Applied: " + model.Key;
            }
            catch (Exception ex)
            {
                model.ErrorText = ex.Message;
                statusText = "Apply failed: " + model.Key;
                logger.LogError(ex);
            }
        }

        private void ApplyAllDirty(List<ConfigEntryModel> dirtyModels)
        {
            int applied = 0;
            int failed = 0;
            int backups = 0;

            foreach (IGrouping<ConfigFile, ConfigEntryModel> group in dirtyModels.GroupBy(x => x.Entry.ConfigFile))
            {
                ConfigFile configFile = group.Key;
                bool oldSaveOnConfigSet = configFile.SaveOnConfigSet;

                try
                {
                    if (settings.CreateBackupBeforeSave.Value)
                    {
                        if (ConfigBackupUtility.TryBackup(configFile, settings.BackupDirectoryName.Value, logger))
                            backups++;
                    }

                    configFile.SaveOnConfigSet = false;

                    foreach (ConfigEntryModel model in group)
                    {
                        try
                        {
                            model.ErrorText = null;
                            model.Entry.SetSerializedValue(model.EditedSerializedValue);
                            model.RefreshFromEntry();
                            applied++;
                        }
                        catch (Exception ex)
                        {
                            model.ErrorText = ex.Message;
                            failed++;
                            logger.LogError(ex);
                        }
                    }

                    configFile.Save();
                }
                catch (Exception ex)
                {
                    failed += group.Count();
                    logger.LogError(ex);
                }
                finally
                {
                    configFile.SaveOnConfigSet = oldSaveOnConfigSet;
                }
            }

            statusText = "Apply Dirty finished. Applied: " + applied + ", Failed: " + failed + ", Backups: " + backups + ".";
        }

        private void ResetOneToDefault(ConfigEntryModel model)
        {
            try
            {
                model.ErrorText = null;

                if (settings.CreateBackupBeforeSave.Value)
                    ConfigBackupUtility.TryBackup(model.Entry.ConfigFile, settings.BackupDirectoryName.Value, logger);

                model.Entry.BoxedValue = model.Entry.DefaultValue;
                model.Entry.ConfigFile.Save();
                model.RefreshFromEntry();

                pendingDangerousModelId = -1;
                statusText = "Reset to default: " + model.Key;
            }
            catch (Exception ex)
            {
                model.ErrorText = ex.Message;
                statusText = "Reset failed: " + model.Key;
                logger.LogError(ex);
            }
        }

        private void RevertAllDirty()
        {
            List<ConfigEntryModel> dirty = allEntries.Where(x => x.IsDirty).ToList();

            foreach (ConfigEntryModel model in dirty)
            {
                model.RevertEdit();
                model.ErrorText = null;
            }

            pendingApplyAllConfirm = false;
            pendingDangerousModelId = -1;
            statusText = "Reverted " + dirty.Count + " dirty edits.";
        }

        private static int CompareEntry(ConfigEntryModel a, ConfigEntryModel b)
        {
            int r = string.Compare(a.PluginName, b.PluginName, StringComparison.OrdinalIgnoreCase);
            if (r != 0)
                return r;

            r = string.Compare(a.Section, b.Section, StringComparison.OrdinalIgnoreCase);
            if (r != 0)
                return r;

            return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
                return false;

            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeToString(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value;
        }

        private static bool ParseBool(string value)
        {
            bool result;
            return bool.TryParse(value, out result) && result;
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(float) ||
                   type == typeof(double);
        }
    }

    internal sealed class ConfigEntryModel
    {
        public int Id;

        public string PluginName;
        public string PluginGuid;
        public string PluginVersion;
        public string PluginKey;
        public string PluginLocation;
        public string ConfigFilePath;

        public string Section;
        public string Key;
        public string Description;

        public Type SettingType;
        public object DefaultValue;

        public ConfigEntryBase Entry;

        public string CurrentSerializedValue;
        public string DefaultSerializedValue;
        public string EditedSerializedValue;

        public bool Hidden;
        public bool RequiresRestart;
        public bool LiveApply;
        public bool Advanced;
        public bool Dangerous;

        public bool HasRange;
        public object MinValue;
        public object MaxValue;
        public string RangeText;

        public bool HasValueList;
        public string[] AcceptableValueStrings;

        public string ErrorText;

        public bool IsDirty
        {
            get { return EditedSerializedValue != CurrentSerializedValue; }
        }

        public string RuntimeStatusLabel
        {
            get
            {
                if (RequiresRestart)
                    return "Restart Required";

                if (LiveApply)
                    return "Live Apply Supported";

                return "Live Apply Unknown";
            }
        }

        public static ConfigEntryModel Create(
            int id,
            string pluginName,
            string pluginGuid,
            string pluginVersion,
            string pluginKey,
            string pluginLocation,
            string configFilePath,
            ConfigEntryBase entry)
        {
            ConfigEntryModel model = new ConfigEntryModel();

            model.Id = id;

            model.PluginName = pluginName;
            model.PluginGuid = pluginGuid;
            model.PluginVersion = pluginVersion;
            model.PluginKey = pluginKey;
            model.PluginLocation = pluginLocation ?? "";
            model.ConfigFilePath = configFilePath ?? "";

            model.Entry = entry;
            model.Section = entry.Definition.Section;
            model.Key = entry.Definition.Key;
            model.Description = entry.Description != null ? entry.Description.Description : "";
            model.SettingType = entry.SettingType;
            model.DefaultValue = entry.DefaultValue;

            model.Hidden = HasAnyTag(entry,
                "HideFromSULFURConfig",
                "HideFromSulfurConfig",
                "HideFromRyukaConfig",
                "HideFromConfigPanel",
                "HideFromREPOConfig",
                "HideREPOConfig");

            model.RequiresRestart = HasAnyTag(entry,
                "SULFURConfigRestartRequired",
                "SulfurConfigRestartRequired",
                "RyukaConfigRestartRequired",
                "RequiresRestart",
                "RestartRequired");

            model.LiveApply = HasAnyTag(entry,
                "SULFURConfigLiveApply",
                "SulfurConfigLiveApply",
                "RyukaConfigLiveApply",
                "LiveApply",
                "RuntimeEditable");

            model.Advanced = HasAnyTag(entry,
                "SULFURConfigAdvanced",
                "SulfurConfigAdvanced",
                "RyukaConfigAdvanced",
                "Advanced");

            model.Dangerous = HasAnyTag(entry,
                "SULFURConfigDangerous",
                "SulfurConfigDangerous",
                "RyukaConfigDangerous",
                "Dangerous",
                "Unsafe");

            model.ReadAcceptableValues();
            model.RefreshFromEntry();

            return model;
        }

        public void RefreshFromEntry()
        {
            CurrentSerializedValue = Entry.GetSerializedValue();
            EditedSerializedValue = CurrentSerializedValue;
            DefaultSerializedValue = ConvertDefaultToSerialized();
            ErrorText = null;
        }

        public void RevertEdit()
        {
            EditedSerializedValue = CurrentSerializedValue;
        }

        private string ConvertDefaultToSerialized()
        {
            if (DefaultValue == null)
                return "";

            if (SettingType == typeof(float))
                return ((float)DefaultValue).ToString(CultureInfo.InvariantCulture);

            if (SettingType == typeof(double))
                return ((double)DefaultValue).ToString(CultureInfo.InvariantCulture);

            if (SettingType == typeof(int))
                return ((int)DefaultValue).ToString(CultureInfo.InvariantCulture);

            if (SettingType == typeof(bool))
                return ((bool)DefaultValue) ? "true" : "false";

            return DefaultValue.ToString();
        }

        private void ReadAcceptableValues()
        {
            HasRange = false;
            HasValueList = false;
            RangeText = null;
            AcceptableValueStrings = null;

            if (Entry.Description == null || Entry.Description.AcceptableValues == null)
                return;

            object acceptable = Entry.Description.AcceptableValues;
            Type acceptableType = acceptable.GetType();

            PropertyInfo minProperty = acceptableType.GetProperty("MinValue");
            PropertyInfo maxProperty = acceptableType.GetProperty("MaxValue");

            if (minProperty != null && maxProperty != null)
            {
                MinValue = minProperty.GetValue(acceptable, null);
                MaxValue = maxProperty.GetValue(acceptable, null);

                if (MinValue != null && MaxValue != null)
                {
                    HasRange = true;
                    RangeText = "Range: " + MinValue + " - " + MaxValue;
                    return;
                }
            }

            PropertyInfo listProperty = acceptableType.GetProperty("AcceptableValues");

            if (listProperty != null)
            {
                object raw = listProperty.GetValue(acceptable, null);
                Array array = raw as Array;

                if (array != null && array.Length > 0)
                {
                    List<string> values = new List<string>();

                    foreach (object item in array)
                    {
                        if (item != null)
                            values.Add(item.ToString());
                    }

                    if (values.Count > 0)
                    {
                        AcceptableValueStrings = values.ToArray();
                        HasValueList = true;
                    }
                }
            }
        }

        private static bool HasAnyTag(ConfigEntryBase entry, params string[] tagNames)
        {
            if (entry == null || entry.Description == null || entry.Description.Tags == null)
                return false;

            foreach (object tag in entry.Description.Tags)
            {
                if (tag == null)
                    continue;

                string tagText = tag as string;
                if (tagText == null)
                    tagText = tag.ToString();

                foreach (string expected in tagNames)
                {
                    if (string.Equals(tagText, expected, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }

    internal static class ConfigBackupUtility
    {
        public static bool TryBackup(ConfigFile configFile, string backupDirectoryName, ManualLogSource logger)
        {
            try
            {
                if (configFile == null || string.IsNullOrWhiteSpace(configFile.ConfigFilePath))
                    return false;

                string sourcePath = configFile.ConfigFilePath;

                if (!File.Exists(sourcePath))
                    return false;

                string sourceDirectory = Path.GetDirectoryName(sourcePath);
                if (string.IsNullOrWhiteSpace(sourceDirectory))
                    return false;

                string folderName = string.IsNullOrWhiteSpace(backupDirectoryName)
                    ? "SULFURConfigBackups"
                    : backupDirectoryName.Trim();

                string backupDirectory = Path.Combine(sourceDirectory, folderName);
                Directory.CreateDirectory(backupDirectory);

                string sourceFileName = Path.GetFileName(sourcePath);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string backupPath = Path.Combine(backupDirectory, sourceFileName + "." + timestamp + ".bak");

                File.Copy(sourcePath, backupPath, false);
                return true;
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.LogWarning("SULFUR Config failed to create cfg backup: " + ex.Message);

                return false;
            }
        }
    }

    internal sealed class GameInputBlocker
    {
        private readonly ManualLogSource logger;
        private readonly List<MapState> states = new List<MapState>();

        private bool locked;

        public GameInputBlocker(ManualLogSource logger)
        {
            this.logger = logger;
        }

        public void Lock(string mapNamesText)
        {
            if (locked)
                return;

            locked = true;
            states.Clear();

            try
            {
                Type inputReaderType = Type.GetType("PerfectRandom.Sulfur.Core.Input.InputReader, Assembly-CSharp");
                if (inputReaderType == null)
                {
                    logger.LogWarning("SULFUR Config could not find InputReader type. Game input lock skipped.");
                    return;
                }

                UnityEngine.Object[] readers = UnityEngine.Object.FindObjectsOfType(inputReaderType);
                if (readers == null || readers.Length == 0)
                {
                    logger.LogInfo("SULFUR Config found no active InputReader instance. Game input lock skipped.");
                    return;
                }

                PropertyInfo inputActionsProperty = inputReaderType.GetProperty(
                    "inputActions",
                    BindingFlags.Public | BindingFlags.Instance);

                if (inputActionsProperty == null)
                {
                    logger.LogWarning("SULFUR Config could not find InputReader.inputActions property. Game input lock skipped.");
                    return;
                }

                string[] mapNames = ParseMapNames(mapNamesText);

                foreach (UnityEngine.Object reader in readers)
                {
                    if (reader == null)
                        continue;

                    object inputActions = inputActionsProperty.GetValue(reader, null);
                    if (inputActions == null)
                        continue;

                    foreach (string mapName in mapNames)
                        TryDisableMap(inputActions, mapName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("SULFUR Config failed to lock game input: " + ex.Message);
            }
        }

        public void Unlock()
        {
            if (!locked)
                return;

            locked = false;

            for (int i = states.Count - 1; i >= 0; i--)
            {
                MapState state = states[i];

                if (!state.WasEnabled)
                    continue;

                try
                {
                    object mapWrapper = GetMapWrapper(state.InputActions, state.MapName);
                    if (mapWrapper == null)
                        continue;

                    MethodInfo enableMethod = mapWrapper.GetType().GetMethod(
                        "Enable",
                        BindingFlags.Public | BindingFlags.Instance);

                    if (enableMethod != null)
                        enableMethod.Invoke(mapWrapper, null);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("SULFUR Config failed to restore input map " + state.MapName + ": " + ex.Message);
                }
            }

            states.Clear();
        }

        private void TryDisableMap(object inputActions, string mapName)
        {
            try
            {
                object mapWrapper = GetMapWrapper(inputActions, mapName);
                if (mapWrapper == null)
                    return;

                bool wasEnabled = GetMapEnabled(mapWrapper);

                states.Add(new MapState
                {
                    InputActions = inputActions,
                    MapName = mapName,
                    WasEnabled = wasEnabled
                });

                if (!wasEnabled)
                    return;

                MethodInfo disableMethod = mapWrapper.GetType().GetMethod(
                    "Disable",
                    BindingFlags.Public | BindingFlags.Instance);

                if (disableMethod != null)
                    disableMethod.Invoke(mapWrapper, null);
            }
            catch (Exception ex)
            {
                logger.LogWarning("SULFUR Config failed to disable input map " + mapName + ": " + ex.Message);
            }
        }

        private static object GetMapWrapper(object inputActions, string mapName)
        {
            if (inputActions == null || string.IsNullOrWhiteSpace(mapName))
                return null;

            PropertyInfo property = inputActions.GetType().GetProperty(
                mapName,
                BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
                return null;

            return property.GetValue(inputActions, null);
        }

        private static bool GetMapEnabled(object mapWrapper)
        {
            if (mapWrapper == null)
                return false;

            PropertyInfo property = mapWrapper.GetType().GetProperty(
                "enabled",
                BindingFlags.Public | BindingFlags.Instance);

            if (property == null)
            {
                property = mapWrapper.GetType().GetProperty(
                    "Enabled",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            if (property == null)
                return true;

            object value = property.GetValue(mapWrapper, null);

            if (value is bool)
                return (bool)value;

            return true;
        }

        private static string[] ParseMapNames(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new[]
                {
                    "OnFoot",
                    "Inventory",
                    "FKeys",
                    "UI",
                    "DevTools"
                };
            }

            return text
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private sealed class MapState
        {
            public object InputActions;
            public string MapName;
            public bool WasEnabled;
        }
    }
}