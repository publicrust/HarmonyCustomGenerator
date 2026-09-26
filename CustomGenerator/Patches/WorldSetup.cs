using HarmonyLib;
using System.Reflection;
using CustomGenerator.Utility;
using UnityEngine;
using System;
using System.IO;

using static CustomGenerator.ExtConfig;
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
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(UI_LoadingScreen), "Update", new Type[] { typeof(string) }); }
        private static void Prefix(ref string strType) {
            if (tempData.terrainTexturing == null || strType != "DONE")  return;
            Logging.Info($"SIZE: {tempData.mapsize} | SEED: {tempData.mapseed}");

            if (Config.Swap.Enabled) {
                // Same getters the game saved the map with, so the path is right with Override Folder/Name on or off
                string path = Path.Combine(World.MapFolderName, World.MapFileName);
                if (File.Exists(path)) SwapMonument.Initiate(path);
                else Logging.Error($"Swap: saved map not found at {path}, swap skipped");
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
            if (Config.Generator.RemovePowerlines) {
                World.Config.Powerlines = false;
                Logging.Generation("Powerlines disabled");
            }
            if (Config.Generator.RemoveTunnels) {
                World.Config.BelowGroundRails = false;
                Logging.Generation("Underground tunnels disabled");
            }
            LoadPercentages();
        }

        // Values are validated in ExtConfig.Validate: non-negative, sums > 0
        private static void LoadPercentages() {
            if (!Config.Generator.ModifyPercentages) return;
            var tier = Config.Generator.Tier;
            var biom = Config.Generator.Biom;

            float tiers = tier.Tier0 + tier.Tier1 + tier.Tier2;
            World.Config.PercentageTier0 = tier.Tier0 / tiers;
            World.Config.PercentageTier1 = tier.Tier1 / tiers;
            World.Config.PercentageTier2 = tier.Tier2 / tiers;

            // Jungle is not part of the biome split (vanilla: 0.4 + 0.15 + 0.15 + 0.3 = 1, jungle 0.5)
            float biomes = biom.Arid + biom.Temperate + biom.Tundra + biom.Arctic;
            World.Config.PercentageBiomeArid = biom.Arid / biomes;
            World.Config.PercentageBiomeTemperate = biom.Temperate / biomes;
            World.Config.PercentageBiomeTundra = biom.Tundra / biomes;
            World.Config.PercentageBiomeArctic = biom.Arctic / biomes;
            World.Config.PercentageBiomeJungle = biom.Jungle / 100f;

            Logging.Generation($"Tier and biome percentages changed");
        }
    }
}
