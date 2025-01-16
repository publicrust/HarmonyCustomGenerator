using UnityEngine;
namespace CustomGenerator
{
    internal class HarmonyModHooks : IHarmonyModHooks
    {
        void IHarmonyModHooks.OnLoaded(OnHarmonyModLoadedArgs args) {
            Debug.Log("[Harmony] Loaded: CustomGenerator");
        }

        void IHarmonyModHooks.OnUnloaded(OnHarmonyModUnloadedArgs args) {
            Debug.Log("[Harmony] Unloaded: CustomGenerator");
        }
    }
}
