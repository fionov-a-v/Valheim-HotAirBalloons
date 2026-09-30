using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace HotAirBalloons
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    public class HotAirBalloonsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "hotairballoons";
        public const string PluginName = "Hot Air Balloons";
        public const string PluginVersion = "1.3.0";

        private Harmony m_harmony;
        private GameObject m_runtime;
        private ConfigFileWatcher m_configWatcher;

        private void Awake()
        {
            BalloonConfig.Bind(Config);
            // Правка .cfg во время игры применяется сразу (а на сервере — рассылается игрокам).
            m_configWatcher = new ConfigFileWatcher(Config);
            BalloonLocalization.Register();

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            PrefabManager.OnPrefabsRegistered += BalloonPieces.ApplyRecipes;
            SynchronizationManager.OnConfigurationSynchronized += (_, _) => BalloonPieces.ApplyRecipes();
            BalloonConfig.RecipesChanged += BalloonPieces.ApplyRecipes;

            m_harmony = new Harmony(PluginGuid);
            m_harmony.PatchAll(typeof(Patches));

            m_runtime = new GameObject("HotAirBalloons_Runtime");
            DontDestroyOnLoad(m_runtime);
            m_runtime.AddComponent<BalloonHud>();

            Jotunn.Logger.LogInfo($"{PluginName} {PluginVersion} загружен");
        }

        private void OnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            BalloonPieces.Register();
        }

        private void OnDestroy()
        {
            m_harmony?.UnpatchSelf();
            if (m_runtime != null)
            {
                Destroy(m_runtime);
            }
        }
    }
}
