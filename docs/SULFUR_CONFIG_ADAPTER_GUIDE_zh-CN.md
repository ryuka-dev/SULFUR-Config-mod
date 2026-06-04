# SULFUR Config 第三方 Mod 适配文档

[English](SULFUR_CONFIG_ADAPTER_GUIDE_en.md) | [简体中文](SULFUR_CONFIG_ADAPTER_GUIDE_zh-CN.md) | [日本語](SULFUR_CONFIG_ADAPTER_GUIDE_ja.md)

## 面向对象

本文面向 **SULFUR 的 BepInEx Mod 开发者**。目标是让你的 Mod 配置能在 **SULFUR Config** 的游戏内原生设置页中更清楚、更易读地显示和编辑。

SULFUR Config 会自动扫描已加载的 BepInEx 插件，并把标准 `.cfg` 配置项转换成游戏内界面。第三方 Mod 通常不需要依赖 SULFUR Config。更好的显示效果来自：清楚的 `ConfigDescription`、合理的范围/选项、可选标签，以及每个 Mod 自己携带的本地化文件。

## 1. 最低适配：正常使用 BepInEx Config

只要你的 Mod 使用标准 BepInEx 配置，SULFUR Config 就可以自动显示。

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

这种方式可以工作，但显示效果比较基础。

## 2. 推荐：使用 ConfigDescription

建议所有公开给玩家的配置项都使用 `ConfigDescription`。

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

## 3. 数值范围：使用 AcceptableValueRange

对于 `int`、`float`、`double`，强烈建议提供合理范围。

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

如果没有范围，SULFUR Config 无法判断玩家应该输入什么数值。

## 4. 固定选项：使用 enum 或 AcceptableValueList

### 推荐：enum

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

### 也可以使用 AcceptableValueList

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

## 5. Bool 和文本配置

Bool 会显示为开关。`string` 会显示为文本输入项。

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

## 7. SULFUR Config 标签适配

SULFUR Config 会读取 `ConfigDescription.Tags`。标签可以直接写字符串，因此第三方 Mod 不需要引用 SULFUR Config 或 SULFUR Native UI Lib。

| 用途 | 推荐标签 | 也可识别 |
|---|---|---|
| 运行中可安全生效 | `LiveApply` | `RuntimeEditable`, `SULFURConfigLiveApply`, `SulfurConfigLiveApply`, `RyukaConfigLiveApply` |
| 需要重启/新开一局 | `RestartRequired` | `RequiresRestart`, `SULFURConfigRestartRequired`, `SulfurConfigRestartRequired`, `RyukaConfigRestartRequired` |
| 高级设置 | `Advanced` | `SULFURConfigAdvanced`, `SulfurConfigAdvanced`, `RyukaConfigAdvanced` |
| 隐藏/调试/内部设置 | `Hidden` | `SULFURConfigHidden`, `SulfurConfigHidden`, `RyukaConfigHidden`, `HideFromSULFURConfig`, `HideFromSulfurConfig`, `HideFromRyukaConfig`, `HideFromConfigPanel`, `HideFromREPOConfig`, `HideREPOConfig` |
| 危险/不安全设置 | `Dangerous` | `Unsafe`, `SULFURConfigDangerous`, `SulfurConfigDangerous`, `RyukaConfigDangerous` |

示例：

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


## 8. 每个 Mod 自己携带本地化

每个 Mod 应该把自己的语言文件放在自己的 DLL 同目录下。

```text
BepInEx/plugins/YourMod/
├─ YourMod.dll
└─ lang/
   ├─ en.json
   ├─ ja.json
   └─ zh-CN.json
```

SULFUR Config 扫描到你的 Mod 时，会读取你的 Mod 自己的 `lang/*.json`。不要把第三方 Mod 的语言文件放到 `SULFURConfig/` 文件夹里。

## 9. 本地化 JSON 格式

当前格式：

```json
{
  "entries": [
    { "key": "plugin.name", "value": "Your Mod Name" },
    { "key": "plugin.description", "value": "Short description shown under this mod." }
  ]
}
```

常用 key：

```text
plugin.name
plugin.description
section.<Section>
entry.<Section>.<Key>.name
entry.<Section>.<Key>.description
value.<Section>.<Key>.<RawValue>
```

示例：

```json
{
  "entries": [
    { "key": "plugin.name", "value": "Deadeye Instinct" },
    { "key": "plugin.description", "value": "提供硬锁定、辅助瞄准和调试相关配置。" },
    { "key": "section.HardLock", "value": "硬锁定" },
    { "key": "entry.HardLock.HardLockMaxDistance.name", "value": "硬锁定最大距离" },
    { "key": "entry.HardLock.HardLockMaxDistance.description", "value": "硬锁定可以选择目标的最大距离。" },
    { "key": "value.Assist.Mode.Off", "value": "关闭" },
    { "key": "value.Assist.Mode.Natural", "value": "自然" },
    { "key": "value.Assist.Mode.Strong", "value": "强" }
  ]
}
```

## 10. 完整 C# 示例

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

## 11. 配置命名建议

推荐：

```text
General.EnableMod
HardLock.HardLockMaxDistance
AutoFire.EnableAutoFire
Debug.EnableDebugLogs
Balance.DamageMultiplier
```

不推荐使用 `a.b`、`test.value`、`tmpSetting`、`Enable`、`Config1` 这类不稳定命名。SULFUR Config 的本地化 key 依赖 `Section` 和 `Key`，频繁改名会导致旧 `lang/*.json` 和旧 `.cfg` 难以维护。

## 12. 是否需要依赖 SULFUR Config？

通常不需要。第三方 Mod 只需要使用标准 BepInEx Config，可选提供 `lang/*.json`，可选在 `ConfigDescription.Tags` 里写字符串标签。

只有当你的 Mod 想主动注册自己的原生 Options 页面时，才需要依赖 `SULFUR Native UI Lib`。

## 13. 运行中修改和保存逻辑

SULFUR Config 使用“待应用”流程：

```text
修改设置 → 显示为“待应用” → 点击“应用” → 写入 cfg
```

如果你的配置支持运行中生效，建议监听 `SettingChanged`：

```csharp
damageMultiplier.SettingChanged += OnDamageMultiplierChanged;

private void OnDamageMultiplierChanged(object sender, EventArgs e)
{
    ApplyDamageMultiplier(damageMultiplier.Value);
}
```

如果不支持运行中修改，请加 `RestartRequired`。

## 14. 适配检查清单

```text
[ ] 所有公开 ConfigEntry 都有清楚说明
[ ] 数值配置使用 AcceptableValueRange
[ ] 固定选项使用 enum 或 AcceptableValueList
[ ] 实时生效配置加 LiveApply
[ ] 需要重启配置加 RestartRequired
[ ] 高级配置加 Advanced
[ ] 调试/内部配置加 Hidden
[ ] 危险配置加 Dangerous
[ ] Mod 目录下有 lang/en.json
[ ] 面向中文/日文玩家时，提供 lang/zh-CN.json / lang/ja.json
[ ] Section 和 Key 命名稳定，不随意重命名
```

## 15. 推荐发布结构

```text
BepInEx/plugins/YourMod/
├─ YourMod.dll
├─ README.md
└─ lang/
   ├─ en.json
   ├─ ja.json
   └─ zh-CN.json
```
