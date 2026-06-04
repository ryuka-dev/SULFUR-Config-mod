# SULFUR Config サードパーティ Mod 対応ガイド

[English](SULFUR_CONFIG_ADAPTER_GUIDE_en.md) | [简体中文](SULFUR_CONFIG_ADAPTER_GUIDE_zh-CN.md) | [日本語](SULFUR_CONFIG_ADAPTER_GUIDE_ja.md)

## 対象者

このドキュメントは、**SULFUR 向け BepInEx Mod 開発者**を対象にしています。目的は、あなたの Mod の設定を **SULFUR Config** のゲーム内ネイティブ Options 画面で、より読みやすく表示・編集できるようにすることです。

SULFUR Config は、読み込まれている BepInEx プラグインを自動でスキャンし、標準的な `.cfg` 設定項目をゲーム内 UI に変換します。通常、サードパーティ Mod は SULFUR Config に依存する必要はありません。表示品質を高めるには、適切な `ConfigDescription`、範囲/選択肢、任意タグ、Mod ごとのローカライズファイルを用意します。

## 1. 最低限の対応：標準 BepInEx Config を使う

通常の BepInEx 設定を使っていれば、SULFUR Config は自動で表示できます。

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

これは動作しますが、表示品質は基本的なものになります。

## 2. 推奨：ConfigDescription を使う

プレイヤーに公開する設定項目には、できるだけ `ConfigDescription` を使ってください。

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

## 3. 数値範囲：AcceptableValueRange を使う

`int`、`float`、`double` には、できるだけ妥当な範囲を設定してください。

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

範囲がない場合、SULFUR Config はプレイヤーにとって妥当な値を判断できません。

## 4. 固定選択肢：enum または AcceptableValueList を使う

### 推奨：enum

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

### 代替：AcceptableValueList

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

## 5. Bool とテキスト設定

Bool はトグルとして表示されます。`string` はテキスト入力として表示されます。

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

## 7. SULFUR Config タグ

SULFUR Config は `ConfigDescription.Tags` を読み取ります。タグは文字列で書けるため、サードパーティ Mod は SULFUR Config や SULFUR Native UI Lib を参照する必要はありません。

| 用途 | 推奨タグ | ほかに認識されるタグ |
|---|---|---|
| 実行中に安全に反映可能 | `LiveApply` | `RuntimeEditable`, `SULFURConfigLiveApply`, `SulfurConfigLiveApply`, `RyukaConfigLiveApply` |
| 再起動/新規ランが必要 | `RestartRequired` | `RequiresRestart`, `SULFURConfigRestartRequired`, `SulfurConfigRestartRequired`, `RyukaConfigRestartRequired` |
| 詳細設定 | `Advanced` | `SULFURConfigAdvanced`, `SulfurConfigAdvanced`, `RyukaConfigAdvanced` |
| 非表示/デバッグ/内部設定 | `Hidden` | `SULFURConfigHidden`, `SulfurConfigHidden`, `RyukaConfigHidden`, `HideFromSULFURConfig`, `HideFromSulfurConfig`, `HideFromRyukaConfig`, `HideFromConfigPanel`, `HideFromREPOConfig`, `HideREPOConfig` |
| 危険/不安全 | `Dangerous` | `Unsafe`, `SULFURConfigDangerous`, `SulfurConfigDangerous`, `RyukaConfigDangerous` |

例：

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


## 8. Mod ごとのローカライズ

各 Mod は、自分の DLL と同じフォルダに `lang/` を置いてください。

```text
BepInEx/plugins/YourMod/
├─ YourMod.dll
└─ lang/
   ├─ en.json
   ├─ ja.json
   └─ zh-CN.json
```

SULFUR Config はその Mod をスキャンしたときに、その Mod 自身の `lang/*.json` を読み込みます。サードパーティ Mod の言語ファイルを `SULFURConfig/` フォルダに入れないでください。

## 9. ローカライズ JSON 形式

現在の形式：

```json
{
  "entries": [
    { "key": "plugin.name", "value": "Your Mod Name" },
    { "key": "plugin.description", "value": "Short description shown under this mod." }
  ]
}
```

よく使う key：

```text
plugin.name
plugin.description
section.<Section>
entry.<Section>.<Key>.name
entry.<Section>.<Key>.description
value.<Section>.<Key>.<RawValue>
```

例：

```json
{
  "entries": [
    { "key": "plugin.name", "value": "Deadeye Instinct" },
    { "key": "plugin.description", "value": "ハードロック、エイム補助、デバッグ設定を提供します。" },
    { "key": "section.HardLock", "value": "ハードロック" },
    { "key": "entry.HardLock.HardLockMaxDistance.name", "value": "ハードロック最大距離" },
    { "key": "entry.HardLock.HardLockMaxDistance.description", "value": "ハードロック対象として選べる最大距離。" },
    { "key": "value.Assist.Mode.Off", "value": "オフ" },
    { "key": "value.Assist.Mode.Natural", "value": "自然" },
    { "key": "value.Assist.Mode.Strong", "value": "強" }
  ]
}
```

## 10. 完全な C# 例

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

## 11. 設定名の推奨

推奨：

```text
General.EnableMod
HardLock.HardLockMaxDistance
AutoFire.EnableAutoFire
Debug.EnableDebugLogs
Balance.DamageMultiplier
```

`a.b`、`test.value`、`tmpSetting`、`Enable`、`Config1` のような不安定な名前は避けてください。ローカライズ key は `Section` と `Key` に依存するため、頻繁な名前変更は古い `lang/*.json` や `.cfg` を壊しやすくします。

## 12. SULFUR Config への依存は必要？

通常は不要です。サードパーティ Mod は標準 BepInEx Config を使い、必要なら `lang/*.json` と `ConfigDescription.Tags` の文字列タグを追加するだけで対応できます。

独自のネイティブ Options ページを登録したい場合のみ、`SULFUR Native UI Lib` に依存してください。

## 13. 実行中変更と保存の流れ

SULFUR Config は「未適用変更」方式です。

```text
設定を変更 → 未適用として表示 → 適用をクリック → cfg に保存
```

実行中に反映できる設定なら、`SettingChanged` を監視することを推奨します。

```csharp
damageMultiplier.SettingChanged += OnDamageMultiplierChanged;

private void OnDamageMultiplierChanged(object sender, EventArgs e)
{
    ApplyDamageMultiplier(damageMultiplier.Value);
}
```

実行中変更に対応していない場合は、`RestartRequired` を付けてください。

## 14. 対応チェックリスト

```text
[ ] すべての公開 ConfigEntry に明確な説明がある
[ ] 数値設定に AcceptableValueRange を使っている
[ ] 固定選択肢に enum または AcceptableValueList を使っている
[ ] 実行中に反映できる設定に LiveApply を付けている
[ ] 再起動が必要な設定に RestartRequired を付けている
[ ] 詳細設定に Advanced を付けている
[ ] デバッグ/内部設定に Hidden を付けている
[ ] 危険な設定に Dangerous を付けている
[ ] Mod フォルダに lang/en.json がある
[ ] 中国語/日本語ユーザー向けに lang/zh-CN.json / lang/ja.json がある
[ ] Section と Key の名前が安定している
```

## 15. 推奨パッケージ構造

```text
BepInEx/plugins/YourMod/
├─ YourMod.dll
├─ README.md
└─ lang/
   ├─ en.json
   ├─ ja.json
   └─ zh-CN.json
```
