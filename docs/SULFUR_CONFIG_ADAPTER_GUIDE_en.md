# SULFUR Config Adapter Guide for Third-Party Mod Developers

[English](SULFUR_CONFIG_ADAPTER_GUIDE_en.md) | [简体中文](SULFUR_CONFIG_ADAPTER_GUIDE_zh-CN.md) | [日本語](SULFUR_CONFIG_ADAPTER_GUIDE_ja.md)

## Target audience

This document is for **SULFUR BepInEx mod developers** who want their mod configuration to display clearly in **SULFUR Config**.

SULFUR Config automatically scans loaded BepInEx plugins and converts standard `.cfg` entries into a readable in-game native Options page. Third-party mods do **not** need to depend on SULFUR Config. Better display quality comes from good `ConfigDescription`, proper ranges/lists, optional tags, and per-mod localization files.

## 1. Minimum support: use standard BepInEx Config

If your mod already uses normal BepInEx config entries, SULFUR Config can display them automatically.

```csharp
private ConfigEntry<bool> enableMod;
private ConfigEntry<float> damageMultiplier;

private void Awake()
{
    enableMod = Config.Bind(
        "General",
        "EnableMod",
        true,
        "Enable this mod."
    );

    damageMultiplier = Config.Bind(
        "Balance",
        "DamageMultiplier",
        1.0f,
        "Damage multiplier applied by this mod."
    );
}
```

This works, but the display quality is basic. The recommended support below gives a much better result.

## 2. Recommended: use ConfigDescription

Use `ConfigDescription` for every public setting.

```csharp
damageMultiplier = Config.Bind(
    "Balance",
    "DamageMultiplier",
    1.0f,
    new ConfigDescription(
        "Multiplier applied to outgoing damage.",
        new AcceptableValueRange<float>(0.1f, 10.0f)
    )
);
```

## 3. Numeric ranges: use AcceptableValueRange

For `int`, `float`, and `double`, always provide a reasonable range.

```csharp
cooldown = Config.Bind(
    "Timing",
    "Cooldown",
    5f,
    new ConfigDescription(
        "Cooldown in seconds.",
        new AcceptableValueRange<float>(0.1f, 60f)
    )
);
```

Without a range, SULFUR Config cannot know what values are reasonable.

## 4. Fixed options: use enum or AcceptableValueList

### Preferred: enum

```csharp
public enum AssistMode
{
    Off,
    Natural,
    Strong
}

private ConfigEntry<AssistMode> assistMode;

assistMode = Config.Bind(
    "Assist",
    "Mode",
    AssistMode.Natural,
    new ConfigDescription("Aim assist behavior.")
);
```

### Alternative: AcceptableValueList

```csharp
private ConfigEntry<string> mode;

mode = Config.Bind(
    "Assist",
    "Mode",
    "Natural",
    new ConfigDescription(
        "Aim assist behavior.",
        new AcceptableValueList<string>("Off", "Natural", "Strong")
    )
);
```

## 5. Bool and text settings

Bool entries are displayed as toggles. String entries are displayed as text input fields.

```csharp
enableAutoFire = Config.Bind(
    "Auto Fire",
    "EnableAutoFire",
    false,
    new ConfigDescription("Enable automatic firing while hard lock conditions are met.")
);

debugKey = Config.Bind(
    "Debug",
    "DebugKey",
    "F8",
    new ConfigDescription("Keyboard key used to open debug tools. Use Unity InputSystem key names, such as F8, F10, Alpha1.")
);
```

## 7. SULFUR Config tags

SULFUR Config reads `ConfigDescription.Tags`. Tags can be plain strings, so your mod does not need to reference SULFUR Config or SULFUR Native UI Lib.

| Purpose | Recommended tag | Also recognized |
|---|---|---|
| Runtime-safe change | `LiveApply` | `RuntimeEditable`, `SULFURConfigLiveApply`, `SulfurConfigLiveApply`, `RyukaConfigLiveApply` |
| Requires restart/new run | `RestartRequired` | `RequiresRestart`, `SULFURConfigRestartRequired`, `SulfurConfigRestartRequired`, `RyukaConfigRestartRequired` |
| Advanced setting | `Advanced` | `SULFURConfigAdvanced`, `SulfurConfigAdvanced`, `RyukaConfigAdvanced` |
| Hidden/debug/internal | `Hidden` | `SULFURConfigHidden`, `SulfurConfigHidden`, `RyukaConfigHidden`, `HideFromSULFURConfig`, `HideFromSulfurConfig`, `HideFromRyukaConfig`, `HideFromConfigPanel`, `HideFromREPOConfig`, `HideREPOConfig` |
| Dangerous/unsafe | `Dangerous` | `Unsafe`, `SULFURConfigDangerous`, `SulfurConfigDangerous`, `RyukaConfigDangerous` |

Example:

```csharp
enemySpawnMultiplier = Config.Bind(
    "Balance",
    "EnemySpawnMultiplier",
    1.0f,
    new ConfigDescription(
        "Multiplier for dynamic enemy spawning.",
        new AcceptableValueRange<float>(0f, 5f),
        "LiveApply",
        "Advanced"
    )
);
```


## 8. Per-mod localization

Each mod should ship its own `lang/` folder next to its DLL.

```text
BepInEx/plugins/YourMod/
├─ YourMod.dll
└─ lang/
   ├─ en.json
   ├─ ja.json
   └─ zh-CN.json
```

SULFUR Config reads the target mod's own `lang/*.json` files when it scans that plugin. Do **not** put third-party mod language files inside the `SULFURConfig/` folder.

## 9. Localization JSON format

Current format:

```json
{
  "entries": [
    { "key": "plugin.name", "value": "Your Mod Name" },
    { "key": "plugin.description", "value": "Short description shown under this mod." }
  ]
}
```

Useful key patterns:

```text
plugin.name
plugin.description
section.<Section>
entry.<Section>.<Key>.name
entry.<Section>.<Key>.description
value.<Section>.<Key>.<RawValue>
```

Example:

```json
{
  "entries": [
    { "key": "plugin.name", "value": "Deadeye Instinct" },
    { "key": "plugin.description", "value": "Configurable hard lock, aim assist, and debug options." },
    { "key": "section.HardLock", "value": "Hard Lock" },
    { "key": "entry.HardLock.HardLockMaxDistance.name", "value": "Hard Lock Max Distance" },
    { "key": "entry.HardLock.HardLockMaxDistance.description", "value": "Maximum target distance for hard lock." },
    { "key": "value.Assist.Mode.Off", "value": "Off" },
    { "key": "value.Assist.Mode.Natural", "value": "Natural" },
    { "key": "value.Assist.Mode.Strong", "value": "Strong" }
  ]
}
```

## 10. Complete C# example

```csharp
using BepInEx;
using BepInEx.Configuration;

namespace YourName.Sulfur.Deadeye
{
    [BepInPlugin("yourname.sulfur.deadeye", "Deadeye Instinct", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public enum AssistMode
        {
            Off,
            Natural,
            Strong
        }

        private ConfigEntry<bool> enableMod;
        private ConfigEntry<AssistMode> mode;
        private ConfigEntry<float> hardLockMaxDistance;
        private ConfigEntry<bool> enableDebugLogs;

        private void Awake()
        {
            enableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                new ConfigDescription(
                    "Enable Deadeye Instinct.",
                    null,
                    "LiveApply"
                )
            );

            mode = Config.Bind(
                "Assist",
                "Mode",
                AssistMode.Natural,
                new ConfigDescription(
                    "Aim assist mode.",
                    null,
                    "LiveApply"
                )
            );

            hardLockMaxDistance = Config.Bind(
                "HardLock",
                "HardLockMaxDistance",
                30f,
                new ConfigDescription(
                    "Maximum target distance for hard lock.",
                    new AcceptableValueRange<float>(1f, 100f),
                    "LiveApply"
                )
            );

            enableDebugLogs = Config.Bind(
                "Debug",
                "EnableDebugLogs",
                false,
                new ConfigDescription(
                    "Enable verbose debug logs.",
                    null,
                    "Hidden"
                )
            );
        }
    }
}
```

## 11. Naming recommendations

Recommended:

```text
General.EnableMod
HardLock.HardLockMaxDistance
AutoFire.EnableAutoFire
Debug.EnableDebugLogs
Balance.DamageMultiplier
```

Avoid unstable names such as `a.b`, `test.value`, `tmpSetting`, `Enable`, or `Config1`. Localization keys depend on `Section` and `Key`, so frequent renames break old `lang/*.json` and old `.cfg` files.

## 12. Does my mod need to depend on SULFUR Config?

Usually, no. Third-party mods only need to use standard BepInEx Config, optionally provide `lang/*.json`, and optionally add string tags in `ConfigDescription.Tags`.

Only depend on `SULFUR Native UI Lib` if your mod wants to register its own custom native Options page.

## 13. Runtime behavior and saving

SULFUR Config uses a pending-change workflow:

```text
Edit setting → Marked as Pending → Click Apply → Write to cfg
```

If your setting supports runtime changes, listen to `SettingChanged`:

```csharp
damageMultiplier.SettingChanged += OnDamageMultiplierChanged;

private void OnDamageMultiplierChanged(object sender, EventArgs e)
{
    ApplyDamageMultiplier(damageMultiplier.Value);
}
```

If it does not support runtime changes, add `RestartRequired`.

## 14. Adapter checklist

```text
[ ] Every public ConfigEntry has a clear description.
[ ] Numeric settings use AcceptableValueRange.
[ ] Fixed options use enum or AcceptableValueList.
[ ] Runtime-safe settings use LiveApply.
[ ] Restart-required settings use RestartRequired.
[ ] Advanced settings use Advanced.
[ ] Debug/internal settings use Hidden.
[ ] Dangerous settings use Dangerous.
[ ] Your mod folder includes lang/en.json.
[ ] If targeting Chinese/Japanese users, include lang/zh-CN.json and/or lang/ja.json.
[ ] Section and Key names are stable and not renamed casually.
```

## 15. Recommended package structure

```text
BepInEx/plugins/YourMod/
├─ YourMod.dll
├─ README.md
└─ lang/
   ├─ en.json
   ├─ ja.json
   └─ zh-CN.json
```
