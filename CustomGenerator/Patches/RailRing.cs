using CustomGenerator.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

using static CustomGenerator.ExtConfig;
namespace CustomGenerator.Generators
{
    [HarmonyPatch]
    class GenerateRailRing_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(GenerateRailRing), "Process"); }
        private static AccessTools.FieldRef<GenerateRailRing, int> MinSize = AccessTools.FieldRefAccess<GenerateRailRing, int>("MinWorldSize");
        private static void Prefix(GenerateRailRing __instance, ref int seed) {
            if (!Config.Generator.Rail.ShouldChange) return;
            if (!Config.Generator.Rail.Enabled) {
                MinSize(__instance) = int.MaxValue;
                Logging.Generation($"RailRing MinWorldSize changed to max!");
            }
            if (!Config.Generator.Rail.GenerateRing) return;

            MinSize(__instance) = 0;
            Logging.Generation($"RailRing MinWorldSize changed to 0!");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            
            List<CodeInstruction> list = instructions.ToList();
            if (!Config.Generator.Rail.GenerateRing || !Config.Generator.Rail.ShouldChange) return list;


            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Ldc_I4)
                {
                    var value = list[i].operand;
                    if (ulong.TryParse(value.ToString(), out ulong size))
                    {
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
    class PlaceMonumentsRailside_Process
    {
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(PlaceMonumentsRailside), "Process"); }
        private static AccessTools.FieldRef<PlaceMonumentsRailside, int> MinSize = AccessTools.FieldRefAccess<PlaceMonumentsRailside, int>("MinWorldSize");
        private static void Prefix(PlaceMonumentsRailside __instance) {
            if (!Config.Generator.Rail.ShouldChange) return;
            if (Config.Generator.Rail.GenerateSideMonuments) return;

            MinSize(__instance) = int.MaxValue;
            Logging.Generation($"RailMonuments MinWorldSize changed to max!");
        }
    }

    
}
