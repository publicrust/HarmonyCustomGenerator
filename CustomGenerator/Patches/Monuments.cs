using CustomGenerator.Utilities;
using CustomGenerator.Utility;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using UnityEngine;

using static CustomGenerator.ExtConfig;
namespace CustomGenerator.Generators
{

    [HarmonyPatch]
    class PlaceMonuments_Process {
        private static AccessTools.FieldRef<PlaceMonuments, PlaceMonuments.DistanceMode> DistanceDifferentType = AccessTools.FieldRefAccess<PlaceMonuments, PlaceMonuments.DistanceMode>("DistanceDifferentType");
        private static AccessTools.FieldRef<PlaceMonuments, PlaceMonuments.DistanceMode> DistanceSameType = AccessTools.FieldRefAccess<PlaceMonuments, PlaceMonuments.DistanceMode>("DistanceSameType");
        private static AccessTools.FieldRef<PlaceMonuments, int> TargetCount = AccessTools.FieldRefAccess<PlaceMonuments, int>("TargetCount");
        private static AccessTools.FieldRef<PlaceMonuments, int> MinWorldSize = AccessTools.FieldRefAccess<PlaceMonuments, int>("MinWorldSize");
        private static AccessTools.FieldRef<PlaceMonuments, int> MinDistanceDifferentType = AccessTools.FieldRefAccess<PlaceMonuments, int>("MinDistanceDifferentType");
        private static AccessTools.FieldRef<PlaceMonuments, int> MinDistanceSameType = AccessTools.FieldRefAccess<PlaceMonuments, int>("MinDistanceSameType");

        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlaceMonuments), nameof(PlaceMonuments.Process)); }
        private static bool Prefix(PlaceMonuments __instance) {
            if (Config.Generator.RemoveTunnelsEntrances && __instance.ResourceFolder == "tunnel-entrance") {
                Logging.Generation("Tunnel Entrances off");
                MinWorldSize(__instance) = 999999;
                //return false;
            }
            if (Config.Generator.UniqueEnviroment.ShouldChange && __instance.ResourceFolder.Contains("unique_environment/"))
            {
                switch (__instance.ResourceFolder.Replace("unique_environment/", ""))
                {
                    case "oasis":
                        {
                            Logging.Generation($"UNIQUE ENVIROMENT - Changing generating oasis to {Config.Generator.UniqueEnviroment.GenerateOasis}");
                            if (Config.Generator.UniqueEnviroment.GenerateOasis) MinWorldSize(__instance) = 0;
                            else MinWorldSize(__instance) = 999999;
                            break;
                        }
                    case "canyon":
                        {
                            Logging.Generation($"UNIQUE ENVIROMENT - Changing generating canyon to {Config.Generator.UniqueEnviroment.GenerateCanyons}");
                            if (Config.Generator.UniqueEnviroment.GenerateCanyons) MinWorldSize(__instance) = 0;
                            else MinWorldSize(__instance) = 999999;
                            break;
                        }
                    case "lake":
                        {
                            Logging.Generation($"UNIQUE ENVIROMENT - Changing generating lake to {Config.Generator.UniqueEnviroment.GenerateLakes}");
                            if (Config.Generator.UniqueEnviroment.GenerateLakes) MinWorldSize(__instance) = 0;
                            else MinWorldSize(__instance) = 999999;
                            break;
                        }
                    default: 
                        break;
                }
            }

            if (!Config.Monuments.Enabled) return true;
            var matchMonuments = Config.Monuments.monuments.Where(x => x.Folder == __instance.ResourceFolder);
            if (!matchMonuments.Any()) return true;

            var monument = matchMonuments.First();

            if (!monument.ShouldChange) return true;
            if (!monument.Generate) return false;

            DistanceDifferentType(__instance) = monument.distanceDifferent;
            DistanceSameType(__instance) = monument.distanceSame;
            MinDistanceDifferentType(__instance) = monument.MinDistanceDifferentType;
            MinDistanceSameType(__instance) = monument.MinDistanceSameType;

            TargetCount(__instance) = monument.TargetCount;
            MinWorldSize(__instance) = monument.MinWorldSize;

            if (monument.Filter.Enabled) {
                __instance.Filter = new SpawnFilter {
                    BiomeType =   monument.Filter.BiomeType.Count == 0 ? (TerrainBiome.Enum)(-1) :    (TerrainBiome.Enum)EnumParser.GetFilterEnum("BiomeType", monument.Filter.BiomeType),
                    SplatType =   monument.Filter.BiomeType.Count == 0 ? (TerrainSplat.Enum)(-1) :    (TerrainSplat.Enum)EnumParser.GetFilterEnum("SplatType", monument.Filter.SplatType),
                    TopologyAll = monument.Filter.BiomeType.Count == 0 ? (TerrainTopology.Enum)(0) :  (TerrainTopology.Enum)EnumParser.GetFilterEnum("TopologyAll", monument.Filter.TopologyAll),
                    TopologyAny = monument.Filter.BiomeType.Count == 0 ? (TerrainTopology.Enum)(-1) : (TerrainTopology.Enum)EnumParser.GetFilterEnum("TopologyAny", monument.Filter.TopologyAny),
                    TopologyNot = monument.Filter.BiomeType.Count == 0 ? (TerrainTopology.Enum)(0) :  (TerrainTopology.Enum)EnumParser.GetFilterEnum("TopologyNot", monument.Filter.TopologyNot),
                };
            }
            //Debug.Log(__instance.TargetCountWorldSizeMultiplier.Evaluate(World.Size));
            //Debug.Log(__instance.TargetCount * __instance.TargetCountWorldSizeMultiplier.Evaluate(World.Size));
            Logging.Generation($"Changed instance values for {monument.Description}");
            return true;
        }
    }

    [HarmonyPatch]
    class PlaceDecorUniform_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlaceDecorUniform), nameof(PlaceDecorUniform.Process)); }
        private static bool Prefix(PlaceDecorUniform __instance)
        {
            if (!Config.Generator.RemoveCarWrecks) return true;
            if (__instance.Description == "Roadside Wrecks") { Logging.Generation("Removing wrecks."); return false; }

            return true;
        }
    }

    [HarmonyPatch]
    class WorldSetup_InitCoroutine
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(WorldSetup), nameof(WorldSetup.InitCoroutine)); }
        private static FieldInfo _monuments = AccessTools.TypeByName("PlaceMonuments").GetField("Monuments", BindingFlags.NonPublic);
        private static bool Prefix(WorldSetup __instance) {
            if (!tempData.shouldGetMonuments || !Config.Monuments.Enabled) return true;
            PlaceMonuments[] placeMonuments = SingletonComponent<WorldSetup>.Instance.GetComponentsInChildren<ProceduralComponent>(true).OfType<PlaceMonuments>().ToArray();
            Logging.Info($"Founded {placeMonuments.Length} PlaceMonuments.");
            Config.Monuments.monuments.Clear();
            foreach (var mon in placeMonuments) {
                Config.Monuments.monuments.Add(new ExtConfig.Monument { 
                    Description = mon.Description, 
                    Folder = mon.ResourceFolder, 
                    distanceDifferent = mon.DistanceDifferentType, 
                    distanceSame = mon.DistanceSameType, 
                    MinDistanceDifferentType = mon.MinDistanceDifferentType, 
                    MinDistanceSameType = mon.MinDistanceSameType,
                    TargetCount = mon.TargetCount,
                    MinWorldSize = mon.MinWorldSize,
                    Filter = new SpawnFilterCfg
                    {
                        Enabled = true,
                        TopologyAll = GetFilterValue(mon.Filter.TopologyAll),
                        TopologyAny = GetFilterValue(mon.Filter.TopologyAny),
                        TopologyNot = GetFilterValue(mon.Filter.TopologyNot),
                        BiomeType   = GetFilterValue(mon.Filter.BiomeType),
                        SplatType   = GetFilterValue(mon.Filter.SplatType),
                    },
                    Generate = true, ShouldChange = true,
                });
            }
            SaveConfig();

            return true;
        }

        private static List<string> GetFilterValue<T>(T enumValue) where T : Enum {
            var result = new List<string>();
            foreach (T value in Enum.GetValues(typeof(T))) {
                if (Convert.ToInt64(value) == 0) continue;
                if (enumValue.HasFlag(value)) { result.Add(value.ToString()); }
            }
            return result;
        }

    }

    //[HarmonyPatch]
    //public static class WorldSetup_InitCoroutine
    //{
    //    private static MethodBase TargetMethod() { return AccessTools.Method(AccessTools.Inner(typeof(WorldSetup), "<InitCoroutine>d__19"), "MoveNext"); }
    //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        List<CodeInstruction> codes = instructions.ToList();
    //        MethodInfo targetMethod = typeof(Component).GetMethod("GetComponentsInChildren", new[] { typeof(bool) }).MakeGenericMethod(typeof(ProceduralComponent));

    //        for (int i = 0; i < codes.Count; i++)
    //        {
    //            if (!(codes[i].opcode == OpCodes.Call && codes[i].operand as MethodInfo == targetMethod)) continue;
    //            if (codes[i + 1].opcode != OpCodes.Stfld) continue;

    //            var components = codes[i + 1].operand;
    //            var ind = i + 2;
    //            //codes.Insert(ind, new CodeInstruction(OpCodes.Ldarg_0));
    //            //codes.Insert(GetIndex(ref ind, true), new CodeInstruction(OpCodes.Ldloc_1));
    //            //codes.Insert(GetIndex(ref ind, true), new CodeInstruction(OpCodes.Ldarg_0));
    //            codes.Insert(GetIndex(ref ind, false), new CodeInstruction(OpCodes.Ldfld, components));
    //            codes.Insert(GetIndex(ref ind), new CodeInstruction(OpCodes.Call, typeof(WorldSetup_InitCoroutine).GetMethod(nameof(ModifyComponents))));
    //            codes.Insert(GetIndex(ref ind), new CodeInstruction(OpCodes.Stfld, components));
    //            break;
    //        }
    //        return codes;

    //        int GetIndex(ref int ind, bool first = false) { if (first) return ind; ind++; return ind; };
    //    }
    //    public static ProceduralComponent[] ModifyComponents(ProceduralComponent[] components)
    //    {
    //        FileLog.Log("components:");
    //        foreach (var comp in components)
    //        {
    //            FileLog.Log(comp.GetType().Name);
    //        }
    //        return components;
    //    }
    //}

}
