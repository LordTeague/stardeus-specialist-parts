using System;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Systems.AI.Combat;
using Game.Systems.Path;
using Game.Utils;
using Game.CodeGen;
using Game.Visuals;
using KL.Grid;
using KL.Randomness;
using KL.Utils;
using UnityEngine;

namespace Game.Systems.AI {
	// Minimally modified version from core sample code for reference.
    public sealed class AIActionDeconstruct : AIAction {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() {
            RegisterAction(new AIActionDeconstruct());
        }
        public const string ActId = "Deconstruct";
        private AIActionDeconstruct() : base(ActId, () => T.ActDeconstruct) {
            Preconditions = AIState
                .With(AIVarsH.IsNear, true)
                .And(AIVarsH.IsDeconstructed, false);

            Outcomes = AIState.With(AIVarsH.IsDeconstructed, true);
            //Further modifications require a better, nonhardcoded way to modify this, but I just want to see if this works.
            Ability externalAbility = Ability.Get("Destroy");
            if (externalAbility != null) {
                WithRequiredAbilities(AbilityIdH.Move, AbilityIdH.Work);
                WithRequiredOneOfAbilities(AbilityIdH.Build, externalAbility.IdH);
            }
            else {
                //Original Code
                WithRequiredAbilities(AbilityIdH.Build, AbilityIdH.Move, AbilityIdH.Work);
				WithoutRequiredOneOfAbilities();
            }
            WithRequiredJobType(JobTypeIdH.Demolition);
        }
        public override void OnActivate(AIAgentComp agent, AIGoal goal, long ticks) {
            agent.BB.Bool1NotSaved = false;
        }
        public override void OnDeactivate(AIAgentComp agent, AIGoal goal, long ticks) {
            agent.BB.Bool1NotSaved = false;
            agent.BB.ClearFX();
        }
        public override AIActionResult Execute(GameState S, AIAgentComp agent, AIGoal goal, long ticks) {
            if (agent.Motion.IsBusy) {
                // stepping aside
                return InProgress;
            }
            if (goal.Target is ConstructableComp c) {
                if (!c.Tile.IsActive) {
                    return Success;
                }
                if (!CheckFloor(agent, c)) {
                    return Failed(T.ErrorDeconstructFloorInUse);
                }
                if (c.LayerId == WorldLayer.Floor && agent.Motion.IsGrounded) {
                    if (!IsSafeToDeconstruct(S, c, agent.Motion.Position.Idx)) {
                        // before failing it, how about we tell the worker to go
                        // somewhere further away
                        if (!agent.Motion.TryStepAside() || agent.TicksInCurrentAction > Consts.TicksPer5Minutes) {
                            agent.Motion.StoreNavFailureAt(c.Tile.PosIdx, Consts.TicksPer10Minutes);
                            return FailedSoft(T.AdRejWouldGetStuck);
                        } else {
                            return InProgress;
                        }
                    }
                }
                if (c.Entity.IsImmutable) {
                    Cancelled(T.AdRejCancelled);
                }
                if (!EntityUtils.IsNearObj(agent.Being.PosIdx, c.ConstructableTile)) {
                    if (!agent.Motion.IsGrounded) {
                        D.Err("Executing AIActionConstruct while flying worker is not near! {0} ({1})",
                            goal, goal.WorldState);
                    }
                    goal.WithWorld(AIVarsH.IsNear, false);
                    return NeedsReplan(T.AdRejUnreachable);
                }
                if (agent.Jobs.TryJob(JobTypeIdH.Demolition, out var slot)) {
                    float workAmount = slot.UseSkill();
                    return PerformWork(S, c, agent, goal, workAmount);
                }
            }
            return Error("Target is not IConstructable: {0}", goal.Target);
        }

        private static bool CheckFloor(AIAgentComp agent, ConstructableComp c) {
            if (!agent.BB.Bool1NotSaved) {
                agent.BB.Bool1NotSaved = true;
                if (c.LayerId == WorldLayer.Floor) {
                    if (EntityUtils.IsObjAt(c.Entity.PosIdx)) {
                        return false;
                    }
                    if (EntityUtils.IsWallAt(c.Entity.PosIdx)) {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool IsSafeToDeconstruct(GameState S, ConstructableComp c, int wp) {
            if (wp == c.Tile.PosIdx) { return false; }
            var areas = S.Sys.Areas;
            // we cannot deconstruct the only tile that leads away from current
            // position
            var walkableN = 0;
            if (areas.IslandAt(Pos.S(wp)) > 0) {
                walkableN++;
            }
            if (areas.IslandAt(Pos.N(wp)) > 0) {
                if (++walkableN > 1) { return true; }
            }
            if (areas.IslandAt(Pos.E(wp)) > 0) {
                if (++walkableN > 1) { return true; }
            }
            if (areas.IslandAt(Pos.W(wp)) > 0) {
                if (++walkableN > 1) { return true; }
            }
            return false;
        }

        private AIActionResult PerformWork(GameState S, ConstructableComp c,
                AIAgentComp agent, AIGoal goal, float workAmount) {
            FaceTarget(c, agent);
            var worker = agent.Being;
            var tile = c.Tile;
            // TODO floor check
            if (c.AddDeconstruction(workAmount)) {
                goal.WithWorld(AIVarsH.IsDeconstructed, true);
                c.BreakDown(2f, moveMaterialsAside: false, orderHaul:
                    S.Prefs.GetBool(Pref.DeconstructOrderHaul,
                        PrefH.DeconstructOrderHaul, false),
                    autoRebuild: false);

                TryStreamline(agent, goal);
                return Success;
            } else {
                ShowFX(S, agent, worker, tile);
            }
            return InProgress;
        }


        public static void ShowFX(GameState S, AIAgentComp agent, Being worker, Tile tile) {
            var punchDir = tile.Position - worker.Position;
            worker.Graphics.Punch(punchDir + Rng.UInsideUnitCircle() * 0.05f,
                0.1f, Rng.URange(0.7f, 1.3f), Rng.URange(0.7f, 1.3f));

            if (agent.BB.FX == null) {
                agent.BB.FX = ParticlesVFX.CreateWithBudget(S,
                    "DeconstructVFX", worker.Position, BBKeyH.SwarmPlayingVFX,
                        Consts.SwarmMaxVFX).WithStaticOwnership(worker);
                if (agent.BB.FX.TryGetFX(out var fx)) {
                    fx.SetVector3(ShaderVars.Target, punchDir);
                    var size = tile.Transform.Size.magnitude * 0.25f;
                    fx.SetFloat(ShaderVars.Size, size);
                    fx.SetFloat(ShaderVars.EmitRate, 200f * size);
                }
                agent.BB.FX.SetClock(S.Clock);
            }

            if (agent.BB.NextSoundTime < GameTimer.realtimeSinceStartup) {
                agent.BB.NextSoundTime = GameTimer.realtimeSinceStartup
                    + 5f / S.Clock.FXTimeScale;
                Sounds.PlayConstruct(worker.Position);
            }
        }
    }
}
