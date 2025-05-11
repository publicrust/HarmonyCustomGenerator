using System.Collections.Generic;
using System;
using System.Reflection;
using System.Reflection.Emit;
using CustomGenerator.Utility;
using HarmonyLib;
using static CustomGenerator.ExtConfig;
using System.Linq;


namespace CustomGenerator.Patches
{
    [HarmonyPatch]
    internal static class GenerateRiverLayout_Patch {  // todo: fix this shit
        private static MethodBase TargetMethod() { return AccessTools.Method(typeof(GenerateRiverLayout), "Process"); }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = instructions.ToList();
            //if (!Config.Generator.ReduceRiversWidth) return list;
            //for (int i = 0; i < list.Count; i++) {
            //    if (list[i].opcode == OpCodes.Ldc_R4 && list[i].operand is float w && Math.Abs(w - 8f) < 0.001f) {
            //        list[i].operand = 5.5f;
            //    }
            //}

            return list;
        }
    }
}
