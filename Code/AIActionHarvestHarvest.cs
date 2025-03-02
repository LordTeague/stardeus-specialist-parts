using System;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Systems.AI;
using Game.Utils;
using Game.CodeGen;
using Game.Visuals;
using KL.Grid;
using KL.Randomness;
using KL.Utils;
using UnityEngine;

namespace Specialist_Parts.AI {
	// Minimally modified version from core sample code for reference.
    public sealed class AIActionHarvestHarvest : AIAction {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() {
            RegisterAction(new AIActionHarvestHarvest());
        }
        public const string ActId = "Harvest";
        private AIActionHarvestHarvest() : base(ActId, () => T.ActHarvestplant) {
            Preconditions = AIState
                .With(AIVarsH.IsNear, true)
                .And(AIVarsH.IsHarvested, false);

            Outcomes = AIState.With(AIVarsH.IsHarvested, true);
            IsQuiet = true;
            //Further modifications require a better, nonhardcoded way to modify this, but I just want to see if this works.
            Ability externalAbility = Ability.Get("Harvest");
            WithRequiredAbilities(externalAbility.IdH, AbilityIdH.Work);
            WithRequiredJobType(JobTypeIdH.Plants);
        }

        public override void OnActivate(AIAgentComp agent, AIGoal goal, long ticks) {
            agent.BB.Float1 = 0f;
        }

        public override void OnDeactivate(AIAgentComp agent, AIGoal goal, long ticks) {
            agent.BB.Float1 = 0f;
            agent.BB.ClearFX();
        }

        public override AIActionResult Execute(GameState S, AIAgentComp agent, AIGoal goal, long ticks) {
            if (goal.Target is PlantComp c) {
                if (!c.Obj.IsActive) {
                    return Failed(T.AdRejTargetUnavailable);
                }
                if (!Pos.IsNear(agent.PosIdx, c.Obj.PosIdx, 1, 1)) {
                    D.Err("Executing AIActionHarvest while worker is not near! {0} ({1})",
                        goal, goal.WorldState);
                    goal.WithWorld(AIVarsH.IsNear, false);
                    return NeedsReplan(T.AdRejUnreachable);
                }
                if (agent.Jobs.TryJob(JobTypeIdH.Plants, out var slot)) {
                    float workAmount = slot.UseSkill();
                    return PerformWork(S, c, agent, goal, workAmount);
                }
            }
            return Error("Wrong target in harvest goal! {0}", goal.Target);
        }

        private AIActionResult PerformWork(GameState S, PlantComp c,
                AIAgentComp agent, AIGoal goal, float workAmount) {
            FaceTarget(c, agent);
            var planter = S.Components.Find<PlanterComp>(c.PlanterId);
            int harvestOutPos;
            if (planter?.Tile.Transform.HasOutPort == true) {
                harvestOutPos = planter.Tile.Transform.OutPort;
            } else {
                harvestOutPos = c.Obj.PosIdx;
            }
            var worker = agent.Being;

            if ((agent.BB.Float1 += workAmount) > 1f) {
                goal.WithWorld(AIVarsH.IsHarvested, true);
                var output = c.Harvest();
                if (output == null) {
                    return Failed("Plant has no harvest output! {0}", c);
                }

                // Drop the harvested stuff on the floor
                for (int i = output.Length - 1; i >= 0; i--) {
                    var mat = output[i];
                    if (mat.StackSize < 1) {
                        D.Err("Harvested mat with stack size {0} from plant {1}. At: {2}",
                            mat, c.Obj, worker.Position);
                        #if UNITY_EDITOR
                        EntityUtils.CameraFocusOn(worker);
                        #endif
                        return Failed("Harvested plant stack size 0! {0} -> {1}",
                            mat, c);
                    }
                    EntityUtils.SpawnRawMaterial(mat, harvestOutPos, 0.1f, true);
                }

                if (Rng.UChance(0.33f)) {
                    worker.Motion.EnqueueDirt(
                        Images.RandomMatching(GraphicsId.DirtDirtSoil01, 2));
                }
                return Success;
            }

            var obj = c.Obj;
            var punchDir = obj.Position - worker.Position;
            worker.Graphics.Punch(punchDir + Rng.UInsideUnitCircle() * 0.05f,
                0.1f, Rng.URange(0.7f, 1.3f), Rng.URange(0.7f, 1.3f));

            if (agent.BB.FX == null) {
                agent.BB.FX = ParticlesVFX.CreateWithBudget(S,
                    "RepairFX", worker.Position, BBKeyH.SwarmPlayingVFX,
                        Consts.SwarmMaxVFX).WithStaticOwnership(worker);
                if (agent.BB.FX.TryGetFX(out var fx)) {
                    fx.SetVector3(ShaderVars.Target, punchDir);
                    var size = obj.SelectableSize.magnitude * 0.25f;
                    fx.SetFloat(ShaderVars.Size, size);
                    fx.SetFloat(ShaderVars.EmitRate, 200f * size);
                }
                agent.BB.FX.SetClock(S.Clock);
            }

            if (agent.BB.NextSoundTime < GameTimer.realtimeSinceStartup) {
                agent.BB.NextSoundTime = GameTimer.realtimeSinceStartup
                    + 5f / S.Clock.FXTimeScale;
                Sounds.PlayRepairs(worker.Position);
            }

            return InProgress;
        }
    }
}
