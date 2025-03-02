using Game.Systems.AI;
using HarmonyLib;
using KL.Collections;
namespace Specialist_Parts.Patches
{
    public static class AIActionDeconstructPatch
    {
        [HarmonyPatch(typeof(AIActionDeconstruct), nameof(AIAction.IsAvailableFor))]
        private static class IsAvailableForPatch 
        {
            private static int moveIdh = Ability.Get("Move").IdH;
            private static int workIdh = Ability.Get("Work").IdH;
            private static int destroyIdh = Ability.Get("Destroy").IdH;
            private static int buildIdh = Ability.Get("Build").IdH;
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result, AIAgentComp agent, AIActionDeconstruct __instance)
            {
                //If being has Destroy ability, patch in that list of abilities, otherwise, reset.
                if (AbilitySource.ExistsFor(agent.BeingData, destroyIdh)) {
                    __instance.RequiredAbilities = new FrugalArray<int>(destroyIdh, moveIdh, workIdh);
                } 
                else {
                    __instance.RequiredAbilities = new FrugalArray<int>(buildIdh, moveIdh, workIdh);
                }
                return true;
            }
        }
    }
}