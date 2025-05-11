using HarmonyLib;
using System.Reflection;
using CustomGenerator.Utility;
using UnityEngine;
using System;
using System.IO;

using static CustomGenerator.ExtConfig;
using CustomGenerator.Utility;
namespace CustomGenerator.Patches {

    [HarmonyPatch]
    internal static class TerrainMeta_Init
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(TerrainMeta), nameof(TerrainMeta.Init)); }

        private static PropertyInfo _terrainPath = AccessTools.TypeByName("TerrainMeta").GetProperty("Path", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static PropertyInfo _terrainTexturing = AccessTools.TypeByName("TerrainMeta").GetProperty("Texturing", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static void Postfix(TerrainMeta __instance) {
            tempData.terrainMeta = __instance;
            tempData.terrainTexturing = (TerrainTexturing)_terrainTexturing.GetValue(__instance);
            tempData.terrainPath = (TerrainPath)_terrainPath.GetValue(__instance);

            if (tempData.terrainPath == null || tempData.terrainTexturing == null || tempData.terrainMeta == null)
                Logging.Error("One of components is null!");
            Logging.Info("Saved TerrainTexturing instance!");
        }
    }

    [HarmonyPatch]
    internal static class LoadingScreen_Update {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(LoadingScreen), "Update", new Type[] { typeof(string) }); }
        private static void Prefix(ref string strType) {
            if (tempData.terrainTexturing == null || strType != "DONE")  return;
            Logging.Info($"SIZE: {tempData.mapsize} | SEED: {tempData.mapseed}");

            if (Config.Swap.Enabled) {
                string path = Path.GetFullPath("maps") + "\\" + string.Format(Config.mapSettings.MapName, tempData.mapsize, tempData.mapseed) + (!Config.mapSettings.MapName.EndsWith(".map") ? ".map" : "");
                SwapMonument.Initiate(path);
            }

            MapImage.RenderMap(0.75f, 150);
            
            //Rust.Application.Quit();
            Application.Quit();
            return;
        }
    }

    [HarmonyPatch]
    internal static class Timing_Start
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(Timing), "Start", new Type[] { typeof(string) }); }
        private static void Prefix(ref string name)
        {
            if (name != "Processing World") return;
            if (Config.Generator.RemoveRivers) {
                World.Config.Rivers = false;
                Logging.Generation("Rivers disabled");
            }
            LoadPercentages();
            Logging.Generation($"Changing tier percentages...");
        }

        private static void LoadPercentages() {
            if (!Config.Generator.ModifyPercentages) return;

            float sum1 = Config.Generator.Tier.Tier0 + Config.Generator.Tier.Tier1 + Config.Generator.Tier.Tier2;
            float sum2 = Config.Generator.Biom.Arid + Config.Generator.Biom.Arctic + Config.Generator.Biom.Temperate + Config.Generator.Biom.Tundra + Config.Generator.Biom.Jungle;

            World.Config.PercentageTier0 = sum1 >= 100f ? Config.Generator.Tier.Tier0 / sum1 : Config.Generator.Tier.Tier0;
            World.Config.PercentageTier1 = sum1 >= 100f ? Config.Generator.Tier.Tier1 / sum1 : Config.Generator.Tier.Tier1;
            World.Config.PercentageTier2 = sum1 >= 100f ? Config.Generator.Tier.Tier2 / sum1 : Config.Generator.Tier.Tier2;

            if (sum1 < 100f)
                Logging.Error("Tier perc. summs lower than 100! Set default.");

            World.Config.PercentageBiomeArid = sum2 >= 100f ? Config.Generator.Biom.Arid / sum2 : Config.Generator.Biom.DefaultArid;
            World.Config.PercentageBiomeArctic = sum2 >= 100f ? Config.Generator.Biom.Arctic / sum2 : Config.Generator.Biom.DefaultArctic;
            World.Config.PercentageBiomeTemperate = sum2 >= 100f ? Config.Generator.Biom.Temperate / sum2 : Config.Generator.Biom.DefaultTemperate;
            World.Config.PercentageBiomeTundra = sum2 >= 100f ? Config.Generator.Biom.Tundra / sum2 : Config.Generator.Biom.DefaultTundra;
            World.Config.PercentageBiomeJungle = sum2 >= 100f ? Config.Generator.Biom.Jungle / sum2 : Config.Generator.Biom.DefaultJungle;

            if (sum2 < 100f)
                Logging.Error("Biom perc. summs lower than 100! Set default.");
        }
    }
}
