using HarmonyLib;
using UnityEngine;
using CustomGenerator.Utilities;

using static CustomGenerator.ExtConfig;
namespace CustomGenerator {
    [HarmonyPatch(typeof(Bootstrap), "StartupShared")]
    internal static class Bootstrap_StartupShared {
        [HarmonyPrefix]
        private static void Prefix() {

            Logging.StartingMessage();
            
            if (Config.SkipAssetWarmup) {
                ConVar.Global.skipAssetWarmup_crashes = true;
                Logging.Info("Skipping asset warmup...");
            }

            Rust.Ai.AiManager.nav_disable = true;
            Rust.Ai.AiManager.nav_wait = false;

            Logging.ClearOldLogs();
        }
    }
}
