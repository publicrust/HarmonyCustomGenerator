using System.Collections.Generic;
using System.Reflection;
using CustomGenerator.Utility;
using HarmonyLib;
using UnityEngine;
using static CustomGenerator.ExtConfig;


namespace CustomGenerator.Patches
{
    [HarmonyPatch]
    internal static class GenerateRiverLayout_Patch {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(GenerateRiverLayout), "Process"); }
        private static AccessTools.FieldRef<TerrainPath, List<PathList>> _rivers = AccessTools.FieldRefAccess<TerrainPath, List<PathList>>("Rivers");

        private static void Prefix(out int __state) {
            __state = _rivers(TerrainMeta.Path).Count;
        }

        private static void Postfix(int __state) {
            float scale = Config.Generator.RiverWidthScale;
            if (World.Networked || Mathf.Approximately(scale, 1f)) return;
            if (scale <= 0f) { Logging.Error($"River width scale must be > 0 (got {scale}), skipping."); return; }

            var rivers = _rivers(TerrainMeta.Path);
            for (int i = __state; i < rivers.Count; i++)
                rivers[i].Width *= scale;

            Logging.Generation($"River width scaled x{scale} for {rivers.Count - __state} rivers");
        }
    }
}
