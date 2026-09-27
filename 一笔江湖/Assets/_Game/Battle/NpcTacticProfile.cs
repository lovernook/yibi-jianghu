using System;
using System.Collections.Generic;
using UnityEngine;
using Yibi.Rules;

namespace Yibi.Battle
{
    public enum NpcTacticCondition { Always, EnergyAtMost, HealthAtMost, WindActive, TargetHasFlaw, ShieldEmpty }

    [Serializable]
    public sealed class NpcTacticRule
    {
        public NpcTacticCondition condition;
        public string skillId = "basic";
        public int threshold;
    }

    [CreateAssetMenu(menuName = "一笔江湖/战斗/敌人套路")]
    public sealed class NpcTacticProfile : ScriptableObject
    {
        public string displayName = "试炼守卫";
        [Tooltip("从上向下匹配；不满足条件、未装备、冷却或内力不足时继续下一项。只改变 NPC 决策，不改变伤害。")]
        public NpcTacticRule[] priorities = DefaultRules(0);
        private static readonly NpcTacticRule[] DefaultStone = DefaultRules(0);
        private static readonly NpcTacticRule[] DefaultWind = DefaultRules(1);

        public string[] ValidateConfiguration()
        {
            var errors = new List<string>();
            if (priorities == null) return new[] { "套路优先级列表为空。" };
            for (int i = 0; i < priorities.Length; i++)
            {
                var rule = priorities[i]; string label = "套路第 " + (i + 1) + " 项";
                if (rule == null) { errors.Add(label + "为空。"); continue; }
                if (Array.IndexOf(BattleContent.Ids, rule.skillId) < 0 && rule.skillId != "rest" && rule.skillId != "basic") errors.Add(label + "使用未知技能 ID：" + rule.skillId);
                if (!Enum.IsDefined(typeof(NpcTacticCondition), rule.condition)) errors.Add(label + "使用未知条件。");
                if (rule.condition == NpcTacticCondition.EnergyAtMost && (rule.threshold < 0 || rule.threshold > 6)) errors.Add(label + "内力阈值应为 0—6。");
                if (rule.condition == NpcTacticCondition.HealthAtMost && (rule.threshold < 0 || rule.threshold > 100)) errors.Add(label + "生命阈值应为 0—100。");
            }
            return errors.ToArray();
        }

        public string Select(BattleState state, int seat = 1) { return Select(state, seat, priorities); }
        public static string SelectDefault(BattleState state, int strategy) { return Select(state, 1, strategy == 1 ? DefaultWind : DefaultStone); }

        public static string Select(BattleState state, int seat, NpcTacticRule[] rules)
        {
            if (state == null || seat < 0 || seat > 1 || state.winner != -2) return "basic";
            // Preview is read-only. It evaluates the current position, not the player's unknown next action.
            var decision = state.Copy();
            decision.activeSeat = seat;
            var actor = decision.fighters[seat];
            var target = decision.fighters[1 - seat];
            if (rules != null) foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrEmpty(rule.skillId)) continue;
                bool matches;
                switch (rule.condition)
                {
                    case NpcTacticCondition.EnergyAtMost: if (rule.threshold < 0 || rule.threshold > 6) continue; matches = actor.energy <= rule.threshold; break;
                    case NpcTacticCondition.HealthAtMost: if (rule.threshold < 0 || rule.threshold > 100) continue; matches = actor.hp <= rule.threshold; break;
                    case NpcTacticCondition.WindActive: matches = actor.windUntil >= actor.ownTurn; break;
                    case NpcTacticCondition.TargetHasFlaw: matches = target.flawUntil >= actor.ownTurn; break;
                    case NpcTacticCondition.ShieldEmpty: matches = actor.shield == 0; break;
                    case NpcTacticCondition.Always: matches = true; break;
                    default: continue;
                }
                if (matches && BattleReducer.CanUse(decision, seat, rule.skillId) == null) return rule.skillId;
            }
            return "basic";
        }

        public static NpcTacticRule[] DefaultRules(int strategy)
        {
            if (strategy == 1) return new[] {
                Rule("rest", NpcTacticCondition.EnergyAtMost, 1),
                Rule("qingxin", NpcTacticCondition.HealthAtMost, 50),
                Rule("liuhuo", NpcTacticCondition.WindActive),
                Rule("lieshi", NpcTacticCondition.TargetHasFlaw),
                Rule("huifeng", NpcTacticCondition.ShieldEmpty),
                Rule("dianxue"), Rule("lieshi"), Rule("liuhuo")
            };
            return new[] { Rule("rest", NpcTacticCondition.EnergyAtMost, 1), Rule("lieshi", NpcTacticCondition.TargetHasFlaw), Rule("dianxue"), Rule("lieshi") };
        }
        static NpcTacticRule Rule(string skill, NpcTacticCondition condition = NpcTacticCondition.Always, int threshold = 0)
        { return new NpcTacticRule { skillId = skill, condition = condition, threshold = threshold }; }
    }
}
