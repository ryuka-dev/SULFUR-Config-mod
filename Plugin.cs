using BepInEx;
using BepInEx.Logging;
using Ryuka.Sulfur.NativeUI;

namespace Ryuka.SulfurConfig
{
    [BepInDependency("ryuka.sulfur.nativeui", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.config";
        public const string PluginName = "SULFUR Config";
        public const string PluginVersion = "0.2.1";

        internal static ManualLogSource Log { get; private set; }

        private ConfigNativePage nativePage;

        private void Awake()
        {
            Log = Logger;

            SulfurLocalization.LoadPluginLocalization(PluginGuid, Info.Location);

            nativePage = new ConfigNativePage(Logger);

            SulfurOptionsApi.RegisterPage(new SulfurOptionsPage
            {
                PageId = PluginGuid,
                DisplayName = "SULFUR Config",
                SortOrder = 9999,
                GetDisplayName = () => L("page.name", "SULFUR Config"),
                BuildPage = nativePage.Build
            });

            Logger.LogInfo("SULFUR Config registered native options page.");
        }

        private void OnDestroy()
        {
            SulfurOptionsApi.UnregisterPage(PluginGuid);

            if (Log == Logger)
                Log = null;
        }

        internal static string L(string key, string fallback)
        {
            return SulfurLocalization.Get(PluginGuid, key, fallback);
        }
    }
}
