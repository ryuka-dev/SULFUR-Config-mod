using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using Ryuka.Sulfur.NativeUI;

namespace Ryuka.SulfurConfig
{
    internal sealed class ConfigEntryModel
    {
        public string PluginGuid;
        public string PluginName;
        public string Section;
        public string Key;
        public string Description;
        public string DisplayName;

        public Type SettingType;
        public ConfigEntryBase Entry;

        public string AppliedValue;
        public string DraftValue;
        public string DefaultValue;

        public bool RequiresRestart;
        public bool LiveApply;
        public bool Advanced;
        public bool Hidden;
        public bool Dangerous;

        public List<string> Badges = new List<string>();
        public List<string> ValueList = new List<string>();

        public float? MinValue;
        public float? MaxValue;
        public string RangeText;

        public bool IsDirty
        {
            get { return AppliedValue != DraftValue; }
        }

        public static ConfigEntryModel Create(
            string pluginGuid,
            string pluginName,
            string section,
            string key,
            ConfigEntryBase entry)
        {
            ConfigEntryModel model = new ConfigEntryModel();

            string originalDescription = entry.Description != null
                ? entry.Description.Description
                : "";

            model.PluginGuid = pluginGuid;
            model.PluginName = pluginName;
            model.Section = section;
            model.Key = key;

            model.DisplayName = SulfurLocalization.Get(
                pluginGuid,
                "entry." + section + "." + key + ".name",
                key);

            model.Description = SulfurLocalization.Get(
                pluginGuid,
                "entry." + section + "." + key + ".description",
                originalDescription);

            model.SettingType = entry.SettingType;
            model.Entry = entry;

            model.AppliedValue = entry.GetSerializedValue();
            model.DraftValue = model.AppliedValue;
            model.DefaultValue = SerializeDefaultValue(entry.DefaultValue, entry.SettingType);

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

            model.Hidden = HasAnyTag(entry,
                "SULFURConfigHidden",
                "SulfurConfigHidden",
                "RyukaConfigHidden",
                "Hidden",
                "HideFromSULFURConfig",
                "HideFromSulfurConfig",
                "HideFromRyukaConfig",
                "HideFromConfigPanel",
                "HideFromREPOConfig",
                "HideREPOConfig");

            model.Dangerous = HasAnyTag(entry,
                "SULFURConfigDangerous",
                "SulfurConfigDangerous",
                "RyukaConfigDangerous",
                "Dangerous",
                "Unsafe");

            model.ReadAcceptableValues();
            model.BuildBadges();

            return model;
        }

        public bool GetBoolDraft()
        {
            bool value;
            return bool.TryParse(DraftValue, out value) && value;
        }

        public float GetFloatDraft()
        {
            float value;

            if (float.TryParse(DraftValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            return 0f;
        }

        public float GetDefaultNumericMin()
        {
            return -999999f;
        }

        public float GetDefaultNumericMax()
        {
            return 999999f;
        }

        public void SetDraft(string value)
        {
            DraftValue = value ?? "";
        }

        public void SetDefaultDraft()
        {
            DraftValue = DefaultValue ?? "";
        }

        public void Apply()
        {
            Entry.SetSerializedValue(DraftValue);
            Entry.ConfigFile.Save();

            AppliedValue = Entry.GetSerializedValue();
            DraftValue = AppliedValue;
        }

        public string GetLocalizedValueLabel(string rawValue)
        {
            return SulfurLocalization.Get(
                PluginGuid,
                "value." + Section + "." + Key + "." + rawValue,
                rawValue);
        }

        private void ReadAcceptableValues()
        {
            if (Entry.Description == null || Entry.Description.AcceptableValues == null)
                return;

            object acceptable = Entry.Description.AcceptableValues;
            Type type = acceptable.GetType();

            PropertyInfo listProp = type.GetProperty("AcceptableValues");

            if (listProp != null)
            {
                Array array = listProp.GetValue(acceptable, null) as Array;

                if (array != null)
                {
                    foreach (object item in array)
                    {
                        if (item != null)
                            ValueList.Add(item.ToString());
                    }
                }
            }

            PropertyInfo minProp = type.GetProperty("MinValue");
            PropertyInfo maxProp = type.GetProperty("MaxValue");

            if (minProp != null && maxProp != null)
            {
                object min = minProp.GetValue(acceptable, null);
                object max = maxProp.GetValue(acceptable, null);

                float minFloat;
                float maxFloat;

                if (TryToFloat(min, out minFloat) && TryToFloat(max, out maxFloat))
                {
                    MinValue = minFloat;
                    MaxValue = maxFloat;
                }
            }
        }

        private void BuildBadges()
        {
            if (SettingType != null)
                Badges.Add(SettingType.Name);

            if (MinValue.HasValue && MaxValue.HasValue)
            {
                RangeText =
                    "Allowed range: " +
                    MinValue.Value.ToString("0.###", CultureInfo.InvariantCulture) +
                    " - " +
                    MaxValue.Value.ToString("0.###", CultureInfo.InvariantCulture);

                Badges.Add("Range");
            }

            if (ValueList.Count > 0)
                Badges.Add("Values: " + ValueList.Count);
        }

        private static string SerializeDefaultValue(object value, Type type)
        {
            if (value == null)
                return "";

            if (type == typeof(float))
                return ((float)value).ToString(CultureInfo.InvariantCulture);

            if (type == typeof(double))
                return ((double)value).ToString(CultureInfo.InvariantCulture);

            if (type == typeof(int))
                return ((int)value).ToString(CultureInfo.InvariantCulture);

            if (type == typeof(bool))
                return ((bool)value) ? "true" : "false";

            return value.ToString();
        }

        private static bool TryToFloat(object value, out float result)
        {
            result = 0f;

            if (value == null)
                return false;

            try
            {
                result = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
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

                string text = tag.ToString();

                foreach (string expected in tagNames)
                {
                    if (string.Equals(text, expected, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
