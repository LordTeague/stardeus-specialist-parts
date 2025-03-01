#if !PRODUCTION_BUILD
// #define DEBUG_AI_ACTIONS
// #define LOG_ACTIONS
#endif
using System;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Systems.Path;
using Game.Systems.Stats;
using Game.Utils;
using Game.CodeGen;
using KL.Collections;
using KL.Grid;
using KL.Utils;
using UnityEngine;
using KL.Randomness;
using Game.Systems.Mental;

namespace Game.Systems.AI {
    // Action descriptor
    // One instance per action type will be shared by every agent
    // NO STATE!!!! STATELESS ONLY!!
    public abstract class AIAction {
        public string Id;
        public int IdH;
        private string nameT;
        private Func<string> nameTFunc;
        public string NameT => nameT ??= nameTFunc == null ? $"act.{Id}".T() : nameTFunc();
        public AIState Preconditions;
        public AIState Outcomes;
        public FrugalArray<int> RequiredAbilities;
        public int RequiredNeed;
        public int JobType;
        public bool DoesNotRequireAbilities;
        public bool IsQuiet;
        public StatFlags FlagsRequired;
        public StatFlags FlagsForbidden;
        #if LOG_ACTIONS
        protected bool DebugLog;
        #endif
        protected static void RegisterAction(AIAction act) {
            Registries.AIActions.Add(act.Id, act);
        }

        protected void Debug(AIAgentComp agent, AIGoal goal, string what) {
            #if LOG_ACTIONS
            if (DebugLog) {
                D.Warn("AIActionDebug: {0} [{1}]: {2} | {3} | {4}", Id,
                    G.Ticks, what, agent, goal);
            }
            #endif
        }
        protected void Debug(AIAgentComp agent, AIGoal goal, string what, params object[] o1) {
            #if LOG_ACTIONS
            if (DebugLog) {
                what = string.Format(what, o1);
                Debug(agent, goal, what);
            }
            #endif
        }

        public AIAction(string id, Func<string> nameT = null) {
            Id = id;
            IdH = Hashes.S(id);
            this.nameTFunc = nameT;
        }
        protected AIAction WithRequiredFlags(StatFlags flags) {
            FlagsRequired = flags;
            return this;
        }
        protected AIAction WithRequiredNeed(int needId) {
            RequiredNeed = needId;
            return this;
        }
        protected AIAction WithForbiddenFlags(StatFlags flags) {
            FlagsForbidden = flags;
            return this;
        }
        protected AIAction WithRequiredJobType(int jobTypeIdH) {
            JobType = jobTypeIdH;
            return this;
        }
        protected AIAction WithoutRequiredAbilities() {
            DoesNotRequireAbilities = true;
            return this;
        }

        protected AIAction WithRequiredAbilities(int a1) {
            RequiredAbilities = new FrugalArray<int>(a1);
            return this;
        }

        protected AIAction WithRequiredAbilities(int a1, int a2) {
            RequiredAbilities = new FrugalArray<int>(a1, a2);
            return this;
        }
        protected AIAction WithRequiredAbilities(int a1, int a2, int a3) {
            RequiredAbilities = new FrugalArray<int>(a1, a2, a3);
            return this;
        }

        public abstract AIActionResult Execute(GameState S, AIAgentComp agent,
            AIGoal goal, long ticks);
        public virtual float CostFor(AIAgentComp agent, AIGoal goal, long ticks) {
            if (goal.Type == AIGoalType.Colony) {
                if (agent.BeingData.Mind.Jobs.TryJob(goal.JobIdH, out var skill)) {
                    return skill.ActionCost(ticks);
                }
                D.Err("Skill not found for agent: {0}. {1}", agent, goal.Job);
                return 1000f;
            }
            if (goal.Type == AIGoalType.Personal || goal.Type == AIGoalType.Combat) {
                return 0.5f;
            }
            // personal goals, etc
            D.Err("Please override CostFor() in AI Action: {0}", this);
            return 1f;
        }

        /// <summary>
        /// This does not get invoked when we save / load
        /// </summary>
        public virtual void OnActivate(AIAgentComp agent, AIGoal goal, long ticks) {
        }

        public virtual void OnDeactivate(AIAgentComp agent, AIGoal goal, long ticks) {
        }

        protected AIActionResult Success {
            get {
                // D.Warn("Success!");
                return AIActionResult.Create(AIActionState.Success);
            }
        }
        protected AIActionResult InProgress => AIActionResult.Create(AIActionState.InProgress);
        protected AIActionResult NeedsReplan(string why) {
            return AIActionResult.Create(AIActionState.NeedsReplan, why);
        }

        protected AIActionResult Cancelled(string why) {
            return AIActionResult.Create(AIActionState.Cancelled, why);
        }
        protected AIActionResult PutBack(string why) {
            return AIActionResult.Create(AIActionState.PutBack, why);
        }
        protected AIActionResult Failed(string why) {
            return AIActionResult.Create(AIActionState.Failed, why);
        }
        protected AIActionResult Failed(string why, object o1) {
            return AIActionResult.Create(AIActionState.Failed, string.Format(why, o1));
        }
        protected AIActionResult Failed(string why, object o1, object o2) {
            return AIActionResult.Create(AIActionState.Failed, string.Format(why, o1, o2));
        }
        protected AIActionResult FailedSoft(string why) {
            return AIActionResult.Create(AIActionState.FailedSoft, why);
        }
        protected AIActionResult FailedUnreachableStorage(AIAgentComp agent, int pos) {
            agent.Motion.StoreNavFailureAt(pos);
            return FailedNavToMat(agent, pos);
        }
        protected AIActionResult FailWithNotifyLackOf(GameState s, MatType type,
                int amt, IMatRequester requester) {
            // It's search for equipment, let's not say "not enough space suit"
                // mostly for EquipmentComp
            if (requester is not IIgnoreMatDeficit) {
                if (!s.Sys.Inventory.HasEnough(type, amt)) {
                    requester.NotifyMatDeficit(type);
                }
            }
            return FailedLackOfMat(type);
        }

        protected AIActionResult FailedLackOfMat(MatType mat) {
            return AIActionResult.Create(AIActionState.Failed,
                mat.LackRejectionT, IconId.CLackingMaterials);
        }
        protected AIActionResult Error(string why) {
            return AIActionResult.Create(AIActionState.Error, why);
        }
        protected AIActionResult Error(string why, object o1) {
            return AIActionResult.Create(AIActionState.Error, string.Format(why, o1));
        }
        protected AIActionResult Error(string why, object o1, object o2) {
            return AIActionResult.Create(AIActionState.Error, string.Format(why, o1, o2));
        }
        protected AIActionResult Error(string why, object o1, object o2, object o3) {
            return AIActionResult.Create(AIActionState.Error, string.Format(why, o1, o2, o3));
        }

        protected AIActionResult FailedNavToMat(AIAgentComp agent, int pos) {
            var worker = agent.Being;
            var isG = worker.Motion.IsGrounded;
            var text = isG ?  T.AdRejUnreachableMatGrounded : T.AdRejUnreachableMatFlying;
            if (pos < 1) {
                D.Err("Failed nav to mat with pos < 1! {0} | {1}", pos, agent.Goal);
            }
            text = $"{text} {Pos.String(pos)} ({Texts.WithColor(worker.Data.Mind.Name.DisplayName, Texts.TMPColorRed)})";
            if (isG) {
                return FailedSoft(text);
            } else {
                return Failed(text);
            }
        }
        protected AIActionResult FailedNavToTarget(AIAgentComp agent,
                bool errOnInvalidPos = true) {
            return FailedNavToTarget(agent, agent.BB.MoveTargetPos,
                errOnInvalidPos);
        }
        protected AIActionResult FailedNavToTarget(AIAgentComp agent, int pos,
                bool errOnInvalidPos = true) {
            if (pos < 1) {
                if (errOnInvalidPos) {
                    D.Err("Trying to store nav failure at position {0}! {1} / {2}", pos,
                        agent.Goal, agent);
                }
                return FailedNavToTargetWithoutStore(agent);
            }
            var worker = agent.Being;
            agent.Motion.StoreNavFailureAt(pos);
            var text = $"{T.AdRejUnreachable} {Pos.String(pos)} ({Texts.WithColor(worker.Data.Mind.Name.DisplayName, Texts.TMPColorRed)})";
            if (agent.Goal.Type == AIGoalType.Colony && agent.Motion.IsGrounded) {
                return FailedSoft(text);
            }
            return Failed(text);
        }
        protected AIActionResult FailedNavToTargetWithoutStore(AIAgentComp agent) {
            var worker = agent.Being;
            var text = $"{T.AdRejUnreachable} ({Texts.WithColor(worker.Data.Mind.Name.DisplayName, Texts.TMPColorRed)})";
            if (agent.Motion.IsGrounded) {
                return FailedSoft(text);
            }
            return Failed(text);
        }
        protected bool TryGoToOnce(AIAgentComp agent, Entity entity, bool willStay,
                PathReqFlags extraFlags = PathReqFlags.None) {
            if (agent.BB.MoveProgress != null) {
                return false;
            }
            if (willStay) {
                extraFlags |= PathReqFlags.TempDestination;
            }
            agent.BB.MoveTargetPos = entity.PosIdx;
            if (entity is Tile t) {
                return agent.Motion.TryGoTo(t, PathReqFlags.AdjNearby | extraFlags,
                    out agent.BB.MoveProgress);
            } else {
                return agent.Motion.TryGoTo(entity.PosIdx,
                    PathReqFlags.AdjOnTop | PathReqFlags.AdjNearby | extraFlags,
                        out agent.BB.MoveProgress);
            }
        }

        protected bool TryGoToOnce(AIAgentComp agent, Tile tile) {
            if (agent.BB.MoveProgress != null) {
                return false;
            }
            agent.BB.MoveTargetPos = tile.PosIdx;
            return agent.Motion.TryGoTo(tile, PathReqFlags.AdjNearby,
                out agent.BB.MoveProgress);
        }

        protected bool TryStreamline(AIAgentComp agent, AIGoal goal) {
            if (goal.Target is not IComponent comp || comp.Entity is not Tile tile) {
                return false;
            }
            if (tile.Transform.IsMulti) {
                foreach (var p in tile.Transform.Outline) {
                    if (TryStreamlineAt(p, agent, goal)) {
                        return true;
                    }
                }
                return false;
            }
            int found = Pos.Around(tile.PosIdx, Caches.PosIdx9, true);
            for (int i = 0; i < found; i++) {
                var pos = Caches.PosIdx9[i];
                if (TryStreamlineAt(pos, agent, goal)) {
                    return true;
                }
            }
            return false;
        }

        private bool TryStreamlineAt(int pos, AIAgentComp agent, AIGoal goal) {
            var neighborTile = EntityUtils.GetTopmostParentTile(pos);
            // streamline?
            var ng = neighborTile?.Goal;
            if (ng == null || ng.JobIdH != goal.JobIdH || ng.Agent != null) {
                return false;
            }
            // streamline!
            if (ng.TrySetAgent(agent)) {
                goal.Flags |= AIGoalFlags.Streamlined;
                ng.ChangeStateTo(AIGoalState.PreAssigned, G.Ticks);
                ng.Flags |= AIGoalFlags.Streamlined;
                // D.Warn("Streamlined a task! {0}=>{1}", agent, ng);
                return true;
            }

            return false;
        }

        protected bool TryGoToOnce(AIAgentComp agent, int posIdx, bool willStay) {
            if (agent.BB.MoveProgress != null) {
                return false;
            }
            agent.BB.MoveTargetPos = posIdx;
            var flags = PathReqFlags.AdjOnTop;
            if (!willStay) {
                flags |= PathReqFlags.TempDestination;
            }
            return agent.Motion.TryGoTo(posIdx, flags, out agent.BB.MoveProgress);
        }

        public override string ToString() {
            return $"AIAct[{Id}]";
        }

        public bool TryUnapply(AIState state, out AIState prevState) {
            if (Outcomes.IsFulfilledIn(state)) {
                // Remove the outcomes
                prevState = state.Subtract(Outcomes);
                // Add preconditions
                prevState = prevState.MergeWith(Preconditions);
                #if DEBUG_AI_ACTIONS
                D.Warn("AIAction {0} can unapply {1} -> {2}", this, state, prevState);
                #endif
                return true;
            } else {
                #if DEBUG_AI_ACTIONS
                D.Warn("AIAction {0} CAN NOT unapply {1}", this, state);
                #endif
            }
            prevState = default;
            return false;
        }

        public bool TryApplyTo(AIState state, out AIState newState) {
            if (Preconditions.IsFulfilledIn(state)) {
                // There is no point to apply this node anymore
                if (Outcomes.IsFulfilledIn(state)) {
                    newState = default;
                    return false;
                }
                // Adding new outcome
                newState = state.MergeWith(Outcomes);
                return true;
            }
            newState = default;
            return false;
        }

        protected void FaceTarget(IComponent target, AIAgentComp agent) {
            if (target == null || target.Entity == null) { return; }
            FacePosition(agent, target.Entity.Position);
        }
        protected void FacePosition(AIAgentComp agent, Vector2 pos) {
            agent.Being.Graphics.SetFacing(Facing.FromDirection(
                pos - agent.Being.Position, agent.Being.Graphics.CurrentFacing));
        }

        public bool IsAvailableFor(AIAgentComp agent) {
            var ra = RequiredAbilities;
            if (JobType != 0) {
                if (agent.Jobs.TryJob(JobType, out var job)) {
                    if (job.Incapable) {
                        return false;
                    }
                } else {
                    return false;
                }
            }
            if (RequiredNeed != 0 && !agent.BeingData.HasNeed(RequiredNeed)) {
                return false;
            }
            if (FlagsRequired != StatFlags.None) {
                if ((FlagsRequired & agent.BeingData.Species.StatFlags) != FlagsRequired) {
                    return false;
                }
            }
            if (FlagsForbidden != StatFlags.None) {
                if ((FlagsForbidden & agent.BeingData.Species.StatFlags) != StatFlags.None) {
                    return false;
                }
            }
            if (ra.Length == 0) {
                if (!DoesNotRequireAbilities) {
                    D.Err("AIAction misconfigured, no RequiredAbilities! Not adding it: {0}",
                        Id);
                    return false;
                }
            } else {
                for (int i = 0; i < ra.Length; i++) {
                    if (!AbilitySource.ExistsFor(agent.BeingData, ra[i])) {
                        return false;
                    }
                }
            }
            return true;
        }

        protected void TryWalkAway(AIAgentComp agent) {
            int foundPos = agent.S.Query.FindNearby.Ask(agent.PosIdx,
                    agent.Motion.IsGrounded, 0,
                    agent.BeingData.Safety.IsFeelingSafeAt);
            if (foundPos == Pos.Invalid) {
                D.Warn("Failed to walk away from pos: {0}", Pos.String(agent.PosIdx));
                return;
            }
            // Move away from current position, post task
            if (agent.Motion.TryGoTo(foundPos,
                    PathReqFlags.AdjOnTop | PathReqFlags.AdjNearby)) {
                agent.Motion.SetForcedWalk();
            }
        }

        protected float CachedRandomCostWithCoolDown(AIAgentBlackboard bb, long ticks,
                int cacheDurationTicks = Consts.TicksPer15Minutes,
                float wFrom = 0.01f, float wTo = 1f) {
            if (bb.IsCoolingDown(IdH, ticks)) {
                return 0f;
            } else {
                return CachedRandomCost(bb, ticks, cacheDurationTicks, wFrom, wTo);
            }
        }

        protected float CachedRandomCost(AIAgentBlackboard bb, long ticks,
                int cacheDurationTicks = Consts.TicksPer15Minutes,
                float wFrom = 0.01f, float wTo = 1f) {
            if (bb.TryGetCachedActionCost(IdH, ticks, out float w)) {
                return w;
            }
            w = Rng.URange(wFrom, wTo);
            bb.SetCachedActionCost(IdH, w, ticks, cacheDurationTicks);
            return w;
        }

        public static void AddMentalEffect(AIAgentComp agent, long ticks,
                int duration, string msg, int effect) {
            if (agent.BeingData.Mind.Health != null) {
                agent.S.Sig.AddMentalEffect.Send(agent.BeingData, MentalEffect.
                    Create(ticks, duration, msg, effect));
            }
        }

        public virtual void OnInterrupt(AIAgentComp agent, AIGoal goal) { }

        public virtual void AfterLoad(AIAgentComp agent, AIGoal goal) { }
    }
}
