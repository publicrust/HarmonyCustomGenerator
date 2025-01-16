using CustomGenerator.Utilities;
using HarmonyLib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

using static CustomGenerator.ExtConfig;
namespace CustomGenerator.Generators
{
    [HarmonyPatch(typeof(World), nameof(World.InitSize), new Type[] { typeof(uint) })]
    internal static class World_InitSize {
        private static uint _size = 0;
        private static void Prefix(ref uint size) {
            if (!Config.mapSettings.OverrideSizes) return;
            tempData.mapsize = size;
            _size = size;

            Logging.Generation("Writed size to convars...");
            if (size > 6000U || size < 1000U) {
                Logging.Generation($"World ({_size}) - Using size bigger or smaller than default, rewriting limits...");
            }
        }
    }

    [HarmonyPatch(typeof(World), nameof(World.InitSeed), new Type[] { typeof(uint) })]
    internal static class World_InitSeed {
        private static void Prefix(ref uint seed) {
            tempData.mapseed = seed;
            Logging.Generation("Writed seed to convars...");
        }
    }

    [HarmonyPatch(typeof(World), "get_Size")]
    public static class World_getSize {
        public static void Postfix(ref uint __result) {
            if (!Config.mapSettings.OverrideSizes) return;
            if (tempData.mapsize == 0) { Debug.Log("map size == 0!"); return; }
            __result = tempData.mapsize;
        }
    }
    [HarmonyPatch(typeof(World), "get_MapFolderName")]
    public static class World_getMapFolderName {
        static readonly string FolderName = "maps";
        static readonly string FolderLocation = Path.GetFullPath(FolderName);
        public static void Postfix(ref string __result) {
            if (!Config.mapSettings.OverrideFolder) return;
            if (!Directory.Exists(FolderName))
                Directory.CreateDirectory(FolderName);

            Logging.Info($"Override save folder to {FolderLocation}");
            __result = FolderLocation;
        }
    }

    [HarmonyPatch(typeof(World), nameof(World.CanLoadFromDisk))]
    public static class GetSizePatch {
        public static void Postfix(ref bool __result) {
            if (!Config.mapSettings.GenerateNewMapEverytime) return;
            __result = false;
        }
    }

    [HarmonyPatch(typeof(World), "get_MapFileName")]
    public static class World_getMapFileName {
        public static void Postfix(ref string __result) {
            if (!Config.mapSettings.OverrideName) return;
            string mapName = string.Format(Config.mapSettings.MapName, tempData.mapsize, tempData.mapseed) + (!Config.mapSettings.MapName.EndsWith(".map") ? ".map" : "");
            Logging.Info($"Override map name to {mapName}");
            __result = mapName;
        }
    }
}