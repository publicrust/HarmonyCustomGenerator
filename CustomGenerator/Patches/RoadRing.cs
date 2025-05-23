using CustomGenerator.Utility;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Collections;
using UnityEngine;

using static CustomGenerator.ExtConfig;
namespace CustomGenerator.Generators {

    [HarmonyPatch]
    class GenerateRoadRing_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(GenerateRoadRing), "Process"); }
        private static AccessTools.FieldRef<GenerateRoadRing, int> MinSize = AccessTools.FieldRefAccess<GenerateRoadRing, int>("MinWorldSize");
        private static void Prefix(GenerateRoadRing __instance, ref int seed) {
            if (!Config.Generator.Road.ShouldChange) return;
            if (!Config.Generator.Road.Enabled) {
                MinSize(__instance) = int.MaxValue;
                Logging.Generation($"Road MinWorldSize changed to max! Dont generate!");
                return;
            }
            if (!Config.Generator.Road.GenerateRing) return;

            MinSize(__instance) = 0;
            Logging.Generation($"Road MinWorldSize changed to 0!");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> list = instructions.ToList();
            if (!Config.Generator.Road.GenerateRing || !Config.Generator.Road.ShouldChange) return list;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldc_I4)
                {
                    var value = list[i].operand;
                    if (ulong.TryParse(value.ToString(), out ulong size)) {
                        if (size != 5000) continue;

                        list[i].operand = 0;
                        break;
                    }
                }
            }
            return list;
        }
    }
    
    [HarmonyPatch]
    class PlaceMonumentsRoadside_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlaceMonumentsRoadside), "Process"); }
        private static AccessTools.FieldRef<PlaceMonumentsRoadside, int> MinSize = AccessTools.FieldRefAccess<PlaceMonumentsRoadside, int>("MinWorldSize");
        private static void Prefix(PlaceMonumentsRoadside __instance, ref int seed) {
            if (!Config.Generator.Road.ShouldChange) return;
            if (Config.Generator.Road.GenerateSideMonuments) return;

            MinSize(__instance) = 99999;
            Logging.Generation($"RoadMonuments MinWorldSize changed to 99999!");
        }
    }

    [HarmonyPatch]
    class PlaceRoadObjects_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlaceRoadObjects), "Process"); }
        private static bool Prefix(PlaceRoadObjects __instance) {
            if (!Config.Generator.Road.ShouldChange) return true;
            if (!Config.Generator.Road.GenerateSideObjects) 
                return false;
            return true;
        }
    }

    [HarmonyPatch]
    internal static class GenerateRoadTopology_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(GenerateRoadTopology), "Process"); }
        private static AccessTools.FieldRef<TerrainTopologyMap, int> _res = AccessTools.FieldRefAccess<TerrainTopologyMap, int>("res");
        private static AccessTools.FieldRef<TerrainTopologyMap, NativeArray<int>> _dst = AccessTools.FieldRefAccess<TerrainTopologyMap, NativeArray<int>>("dst");

        private static void Postfix() {
            if (!Config.Generator.AllowRoadBuild) return;
            TerrainHeightMap heightmap = TerrainMeta.HeightMap;
            TerrainTopologyMap topomap = TerrainMeta.TopologyMap;
            NativeArray<int> map = _dst(topomap);
            int res = _res(topomap);

            ImageProcessing.Dilate2D(map, res, res, TerrainTopology.ROAD, 1, delegate (int x, int y) {
                if ((map[x * res + y] & 49) != 0)
                {
                    map[x * res + y] &= ~TerrainTopology.ROAD;
                    map[x * res + y] |= TerrainTopology.BUILDING;
                }
            });
        }
    }
}
