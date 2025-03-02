using Game.Systems.AI;
using HarmonyLib;
using KL.Collections;
namespace Specialist_Parts.Patches
{
    public static class AIActionHarvestPatch
    {
        [HarmonyPatch(typeof(AIActionHarvest), nameof(AIAction.IsAvailableFor))]
        private static class IsAvailableForPatch 
        {
            private static int workIdh = Ability.Get("Work").IdH;
            private static int harvestIdh = Ability.Get("Harvest").IdH;
            private static int manipulateIdh = A0bility.Get("Manipulate").IdH;
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result, AIAgentComp agent, AIActionHarvest __instance)
            {
                //If being has Harvest ability, patch in that list of abilities, otherwise, reset.
                if (AbilitySource.ExistsFor(agent.BeingData, harvestIdh)) {
                    __instance.RequiredAbilities = new FrugalArray<int>(harvestIdh, workIdh);
                } 
                else {
                    __instance.RequiredAbilities = new FrugalArray<int>(manipulateIdh, workIdh);
                }
                return true;
            }
        }
    }
}