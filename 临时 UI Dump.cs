using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

using UICanvas = UnityEngine.Canvas;
using UICanvasScaler = UnityEngine.UI.CanvasScaler;
using UIButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;
using UIText = UnityEngine.UI.Text;
using UISelectable = UnityEngine.UI.Selectable;
using UIScrollRect = UnityEngine.UI.ScrollRect;
using UILayoutGroup = UnityEngine.UI.LayoutGroup;
using UIContentSizeFitter = UnityEngine.UI.ContentSizeFitter;
using UIEventSystem = UnityEngine.EventSystems.EventSystem;

namespace Ryuka.SulfurUIDumper
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.ui_dumper";
        public const string PluginName = "SULFUR UI Dumper";
        public const string PluginVersion = "0.1.0";

        private ConfigEntry<bool> dumpAutomatically;
        private ConfigEntry<float> autoDumpDelaySeconds;
        private ConfigEntry<string> manualDumpKey;
        private ConfigEntry<bool> dumpAllCanvases;
        private ConfigEntry<bool> dumpInactiveObjects;
        private ConfigEntry<bool> includeTransformChildren;
        private ConfigEntry<bool> includeComponentFields;
        private ConfigEntry<int> maxFieldStringLength;

        private Key cachedManualDumpKey = Key.F8;

        private void Awake()
        {
            dumpAutomatically = Config.Bind(
                "General",
                "DumpAutomatically",
                true,
                "Dump UI hierarchy automatically after CanvasManager creates pauseMenu/optionsScreen."
            );

            autoDumpDelaySeconds = Config.Bind(
                "General",
                "AutoDumpDelaySeconds",
                3f,
                new ConfigDescription(
                    "Extra delay after CanvasManager is detected before dumping.",
                    new AcceptableValueRange<float>(0f, 30f)
                )
            );

            manualDumpKey = Config.Bind(
                "General",
                "ManualDumpKey",
                "F8",
                "Keyboard key used to manually dump UI hierarchy."
            );

            dumpAllCanvases = Config.Bind(
                "Dump",
                "DumpAllCanvases",
                true,
                "Also dump every Canvas found in the scene."
            );

            dumpInactiveObjects = Config.Bind(
                "Dump",
                "DumpInactiveObjects",
                true,
                "Include inactive children when dumping target hierarchies."
            );

            includeTransformChildren = Config.Bind(
                "Dump",
                "IncludeTransformChildren",
                true,
                "Include child count and sibling index."
            );

            includeComponentFields = Config.Bind(
                "Dump",
                "IncludeComponentFields",
                true,
                "Include selected component details such as Button, Text, TMP_Text, RectTransform, LayoutGroup, ScrollRect."
            );

            maxFieldStringLength = Config.Bind(
                "Dump",
                "MaxFieldStringLength",
                240,
                new ConfigDescription(
                    "Maximum length for one dumped text field.",
                    new AcceptableValueRange<int>(40, 1000)
                )
            );

            RebuildManualKey();

            Config.SettingChanged += OnConfigChanged;

            if (dumpAutomatically.Value)
            {
                StartCoroutine(AutoDumpRoutine());
            }

            Logger.LogInfo("SULFUR UI Dumper loaded. Press " + cachedManualDumpKey + " to dump UI hierarchy.");
        }

        private void OnDestroy()
        {
            Config.SettingChanged -= OnConfigChanged;
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            try
            {
                if (Keyboard.current[cachedManualDumpKey].wasPressedThisFrame)
                {
                    DumpNow("manual");
                }
            }
            catch
            {
            }
        }

        private void OnConfigChanged(object sender, SettingChangedEventArgs args)
        {
            if (args != null && args.ChangedSetting == manualDumpKey)
            {
                RebuildManualKey();
            }
        }

        private void RebuildManualKey()
        {
            Key parsed;
            if (Enum.TryParse(manualDumpKey.Value, true, out parsed))
            {
                cachedManualDumpKey = parsed;
                return;
            }

            cachedManualDumpKey = Key.F8;
            Logger.LogWarning("Invalid ManualDumpKey. Falling back to F8.");
        }

        private IEnumerator AutoDumpRoutine()
        {
            float start = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - start < 180f)
            {
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                bool hasCanvasManager = TryGetCanvasManagerObjects(out _, out GameObject pauseMenu, out GameObject optionsScreen);

                if (sceneName != "MainMenu" && hasCanvasManager && pauseMenu != null)
                {
                    break;
                }

                yield return null;
            }

            if (autoDumpDelaySeconds.Value > 0f)
                yield return new WaitForSecondsRealtime(autoDumpDelaySeconds.Value);

            DumpNow("auto_ingame");
        }

        private void DumpNow(string reason)
        {
            try
            {
                string dir = Path.Combine(Paths.ConfigPath, "SULFURUIDumps");
                Directory.CreateDirectory(dir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string path = Path.Combine(dir, "SULFUR_UI_Dump_" + reason + "_" + timestamp + ".txt");

                StringBuilder sb = new StringBuilder(1024 * 512);

                sb.AppendLine("SULFUR UI Dumper");
                sb.AppendLine("Version: " + PluginVersion);
                sb.AppendLine("Reason: " + reason);
                sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sb.AppendLine("Unity: " + Application.unityVersion);
                sb.AppendLine("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                sb.AppendLine();

                DumpCurrentLanguage(sb);
                DumpEventSystem(sb);
                DumpCanvasManagerTargets(sb);

                if (dumpAllCanvases.Value)
                {
                    DumpAllCanvases(sb);
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                Logger.LogInfo("SULFUR UI dump written to: " + path);
            }
            catch (Exception ex)
            {
                Logger.LogError("SULFUR UI dump failed: " + ex);
            }
        }

        private void DumpCurrentLanguage(StringBuilder sb)
        {
            sb.AppendLine("=== Localization ===");

            try
            {
                Type localizationManagerType = FindTypeByFullName("I2.Loc.LocalizationManager");

                if (localizationManagerType == null)
                {
                    sb.AppendLine("LocalizationManager: not found");
                    sb.AppendLine();
                    return;
                }

                PropertyInfo currentLanguage = localizationManagerType.GetProperty(
                    "CurrentLanguage",
                    BindingFlags.Public | BindingFlags.Static);

                object value = currentLanguage != null ? currentLanguage.GetValue(null, null) : null;

                sb.AppendLine("LocalizationManager.CurrentLanguage: " + Safe(value));
            }
            catch (Exception ex)
            {
                sb.AppendLine("Localization dump failed: " + ex.Message);
            }

            sb.AppendLine();
        }

        private static Type FindTypeByFullName(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(fullName);
                    if (type != null)
                        return type;
                }
                catch
                {
                }
            }

            return null;
        }

        private void DumpEventSystem(StringBuilder sb)
        {
            sb.AppendLine("=== EventSystem ===");

            try
            {
                var eventSystem = UIEventSystem.current;

                if (eventSystem == null)
                {
                    sb.AppendLine("EventSystem.current: null");
                    sb.AppendLine();
                    return;
                }

                sb.AppendLine("Path: " + GetPath(eventSystem.transform));
                sb.AppendLine("sendNavigationEvents: " + eventSystem.sendNavigationEvents);
                sb.AppendLine("firstSelectedGameObject: " + Safe(eventSystem.firstSelectedGameObject));
                sb.AppendLine("currentSelectedGameObject: " + Safe(eventSystem.currentSelectedGameObject));
                sb.AppendLine("currentInputModule: " + Safe(eventSystem.currentInputModule));
            }
            catch (Exception ex)
            {
                sb.AppendLine("EventSystem dump failed: " + ex.Message);
            }

            sb.AppendLine();
        }

        private void DumpCanvasManagerTargets(StringBuilder sb)
        {
            sb.AppendLine("=== CanvasManager Targets ===");

            try
            {
                if (!TryGetCanvasManagerObjects(out Component canvasManager, out GameObject pauseMenu, out GameObject optionsScreen))
                {
                    sb.AppendLine("CanvasManager / pauseMenu / optionsScreen not found.");
                    sb.AppendLine();
                    return;
                }

                sb.AppendLine("CanvasManager: " + GetPath(canvasManager.transform));
                sb.AppendLine();

                GameObject nonScalingCanvas = GetFieldGameObject(canvasManager, "nonScalingCanvas");
                GameObject canvas = GetFieldGameObject(canvasManager, "canvas");

                if (canvas != null)
                {
                    sb.AppendLine("--- CanvasManager.canvas ---");
                    DumpHierarchy(canvas.transform, sb, 0);
                    sb.AppendLine();
                }

                if (nonScalingCanvas != null)
                {
                    sb.AppendLine("--- CanvasManager.nonScalingCanvas ---");
                    DumpHierarchy(nonScalingCanvas.transform, sb, 0);
                    sb.AppendLine();
                }

                if (pauseMenu != null)
                {
                    sb.AppendLine("--- CanvasManager.pauseMenu ---");
                    DumpHierarchy(pauseMenu.transform, sb, 0);
                    sb.AppendLine();
                }

                if (optionsScreen != null)
                {
                    sb.AppendLine("--- CanvasManager.optionsScreen ---");
                    DumpHierarchy(optionsScreen.transform, sb, 0);
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("CanvasManager target dump failed: " + ex);
            }

            sb.AppendLine();
        }

        private void DumpAllCanvases(StringBuilder sb)
        {
            sb.AppendLine("=== All Canvases ===");

            try
            {
                UICanvas[] canvases = Resources.FindObjectsOfTypeAll<UICanvas>();

                foreach (Canvas canvas in canvases)
                {
                    if (canvas == null)
                        continue;

                    if (!IsSceneObject(canvas.gameObject))
                        continue;

                    sb.AppendLine("--- Canvas: " + GetPath(canvas.transform) + " ---");
                    DumpHierarchy(canvas.transform, sb, 0);
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("All canvas dump failed: " + ex);
            }

            sb.AppendLine();
        }

        private bool TryGetCanvasManagerObjects(out Component canvasManager, out GameObject pauseMenu, out GameObject optionsScreen)
        {
            canvasManager = null;
            pauseMenu = null;
            optionsScreen = null;

            Type canvasManagerType = FindTypeByFullName("PerfectRandom.Sulfur.Core.CanvasManager");
            if (canvasManagerType == null)
                return false;

            UnityEngine.Object[] managers = Resources.FindObjectsOfTypeAll(canvasManagerType);

            if (managers == null || managers.Length == 0)
                return false;

            foreach (UnityEngine.Object obj in managers)
            {
                Component component = obj as Component;
                if (component == null)
                    continue;

                if (!IsSceneObject(component.gameObject))
                    continue;

                canvasManager = component;
                pauseMenu = GetFieldGameObject(component, "pauseMenu");
                optionsScreen = GetFieldGameObject(component, "optionsScreen");

                if (pauseMenu != null || optionsScreen != null)
                    return true;
            }

            return canvasManager != null;
        }

        private static GameObject GetFieldGameObject(Component component, string fieldName)
        {
            if (component == null)
                return null;

            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
                return null;

            object value = field.GetValue(component);

            if (value is GameObject go)
                return go;

            if (value is Component c)
                return c.gameObject;

            return null;
        }

        private void DumpHierarchy(Transform transform, StringBuilder sb, int depth)
        {
            if (transform == null)
                return;

            if (!dumpInactiveObjects.Value && !transform.gameObject.activeInHierarchy)
                return;

            string indent = new string(' ', depth * 2);
            GameObject go = transform.gameObject;

            sb.Append(indent);
            sb.Append(go.name);
            sb.Append(" | path=");
            sb.Append(GetPath(transform));
            sb.Append(" | activeSelf=");
            sb.Append(go.activeSelf);
            sb.Append(" | activeInHierarchy=");
            sb.Append(go.activeInHierarchy);
            sb.Append(" | layer=");
            sb.Append(LayerMask.LayerToName(go.layer));
            sb.AppendLine();

            if (includeTransformChildren.Value)
            {
                sb.Append(indent);
                sb.Append("  siblingIndex=");
                sb.Append(transform.GetSiblingIndex());
                sb.Append(" childCount=");
                sb.Append(transform.childCount);
                sb.AppendLine();
            }

            DumpComponents(go, sb, indent + "  ");

            for (int i = 0; i < transform.childCount; i++)
            {
                DumpHierarchy(transform.GetChild(i), sb, depth + 1);
            }
        }

        private void DumpComponents(GameObject go, StringBuilder sb, string indent)
        {
            Component[] components = go.GetComponents<Component>();

            sb.Append(indent);
            sb.Append("Components: ");

            for (int i = 0; i < components.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                sb.Append(components[i] == null ? "MissingScript" : components[i].GetType().FullName);
            }

            sb.AppendLine();

            if (!includeComponentFields.Value)
                return;

            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                DumpImportantComponent(component, sb, indent);
            }
        }

        private void DumpImportantComponent(Component component, StringBuilder sb, string indent)
        {
            try
            {
                if (component is RectTransform rt)
                {
                    sb.AppendLine(indent + "[RectTransform]");
                    sb.AppendLine(indent + "  anchorMin=" + rt.anchorMin + " anchorMax=" + rt.anchorMax);
                    sb.AppendLine(indent + "  pivot=" + rt.pivot + " sizeDelta=" + rt.sizeDelta);
                    sb.AppendLine(indent + "  anchoredPosition=" + rt.anchoredPosition);
                    sb.AppendLine(indent + "  offsetMin=" + rt.offsetMin + " offsetMax=" + rt.offsetMax);
                    return;
                }

                if (component is UICanvas canvas)
                {
                    sb.AppendLine(indent + "[Canvas]");
                    sb.AppendLine(indent + "  renderMode=" + canvas.renderMode + " sortingOrder=" + canvas.sortingOrder);
                    sb.AppendLine(indent + "  overrideSorting=" + canvas.overrideSorting + " pixelPerfect=" + canvas.pixelPerfect);
                    return;
                }

                if (component is UICanvasScaler scaler)
                {
                    sb.AppendLine(indent + "[CanvasScaler]");
                    sb.AppendLine(indent + "  uiScaleMode=" + scaler.uiScaleMode);
                    sb.AppendLine(indent + "  referenceResolution=" + scaler.referenceResolution);
                    sb.AppendLine(indent + "  matchWidthOrHeight=" + scaler.matchWidthOrHeight);
                    return;
                }

                if (component is UIButton button)
                {
                    sb.AppendLine(indent + "[Button]");
                    sb.AppendLine(indent + "  interactable=" + button.interactable);
                    sb.AppendLine(indent + "  transition=" + button.transition);
                    sb.AppendLine(indent + "  navigation=" + button.navigation.mode);
                    sb.AppendLine(indent + "  targetGraphic=" + Safe(button.targetGraphic));
                    sb.AppendLine(indent + "  onClickPersistentCount=" + button.onClick.GetPersistentEventCount());
                    return;
                }

                if (component is UISelectable selectable)
                {
                    sb.AppendLine(indent + "[Selectable]");
                    sb.AppendLine(indent + "  interactable=" + selectable.interactable);
                    sb.AppendLine(indent + "  transition=" + selectable.transition);
                    sb.AppendLine(indent + "  navigation=" + selectable.navigation.mode);
                    sb.AppendLine(indent + "  targetGraphic=" + Safe(selectable.targetGraphic));
                    return;
                }

                if (component is UIText text)
                {
                    sb.AppendLine(indent + "[Text]");
                    sb.AppendLine(indent + "  text=" + Clip(text.text));
                    sb.AppendLine(indent + "  font=" + Safe(text.font));
                    sb.AppendLine(indent + "  fontSize=" + text.fontSize);
                    sb.AppendLine(indent + "  alignment=" + text.alignment);
                    return;
                }

                if (component is UIImage image)
                {
                    sb.AppendLine(indent + "[Image]");
                    sb.AppendLine(indent + "  sprite=" + Safe(image.sprite));
                    sb.AppendLine(indent + "  color=" + image.color);
                    sb.AppendLine(indent + "  type=" + image.type);
                    sb.AppendLine(indent + "  raycastTarget=" + image.raycastTarget);
                    return;
                }

                if (component is UIScrollRect scrollRect)
                {
                    sb.AppendLine(indent + "[ScrollRect]");
                    sb.AppendLine(indent + "  horizontal=" + scrollRect.horizontal + " vertical=" + scrollRect.vertical);
                    sb.AppendLine(indent + "  viewport=" + SafeTransform(scrollRect.viewport));
                    sb.AppendLine(indent + "  content=" + SafeTransform(scrollRect.content));
                    sb.AppendLine(indent + "  movementType=" + scrollRect.movementType);
                    return;
                }

                if (component is UILayoutGroup layoutGroup)
                {
                    sb.AppendLine(indent + "[LayoutGroup]");
                    sb.AppendLine(indent + "  padding=" + layoutGroup.padding);
                    sb.AppendLine(indent + "  childAlignment=" + layoutGroup.childAlignment);
                    return;
                }

                if (component is UIContentSizeFitter fitter)
                {
                    sb.AppendLine(indent + "[ContentSizeFitter]");
                    sb.AppendLine(indent + "  horizontalFit=" + fitter.horizontalFit + " verticalFit=" + fitter.verticalFit);
                    return;
                }

                if (TryDumpTMPText(component, sb, indent))
                    return;
            }
            catch (Exception ex)
            {
                sb.AppendLine(indent + "[" + component.GetType().Name + "] dump failed: " + ex.Message);
            }
        }

        private bool TryDumpTMPText(Component component, StringBuilder sb, string indent)
        {
            Type type = component.GetType();

            if (type.FullName != "TMPro.TextMeshProUGUI" &&
                type.FullName != "TMPro.TextMeshPro" &&
                type.FullName != "TMPro.TMP_Text")
            {
                if (!IsSubclassOf(type, "TMPro.TMP_Text"))
                    return false;
            }

            sb.AppendLine(indent + "[TMP_Text]");

            object text = GetProperty(component, "text");
            object fontSize = GetProperty(component, "fontSize");
            object alignment = GetProperty(component, "alignment");
            object font = GetProperty(component, "font");

            sb.AppendLine(indent + "  text=" + Clip(text != null ? text.ToString() : ""));
            sb.AppendLine(indent + "  font=" + Safe(font));
            sb.AppendLine(indent + "  fontSize=" + Safe(fontSize));
            sb.AppendLine(indent + "  alignment=" + Safe(alignment));

            return true;
        }

        private static bool IsSubclassOf(Type type, string fullName)
        {
            while (type != null)
            {
                if (type.FullName == fullName)
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        private static object GetProperty(object obj, string propertyName)
        {
            if (obj == null)
                return null;

            PropertyInfo property = obj.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);

            return property != null ? property.GetValue(obj, null) : null;
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            StringBuilder sb = new StringBuilder(transform.name);
            Transform current = transform.parent;

            while (current != null)
            {
                sb.Insert(0, current.name + "/");
                current = current.parent;
            }

            return sb.ToString();
        }

        private static bool IsSceneObject(GameObject go)
        {
            if (go == null)
                return false;

            return go.scene.IsValid();
        }

        private static string Safe(object value)
        {
            if (value == null)
                return "<null>";

            if (value is UnityEngine.Object unityObject)
            {
                if (unityObject == null)
                    return "<destroyed>";

                return unityObject.name + " (" + unityObject.GetType().Name + ")";
            }

            return value.ToString();
        }

        private static string SafeTransform(Transform transform)
        {
            return transform == null ? "<null>" : GetPath(transform);
        }

        private string Clip(string value)
        {
            if (value == null)
                return "";

            value = value.Replace("\r", "\\r").Replace("\n", "\\n");

            int max = Mathf.Clamp(maxFieldStringLength.Value, 40, 1000);

            if (value.Length <= max)
                return value;

            return value.Substring(0, max) + "...";
        }
    }
}