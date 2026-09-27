using System;
using System.Collections.Generic;
using UnityEngine;
using Yibi.Rules;

namespace Yibi.Battle
{
    [Serializable]
    public sealed class TechniquePresentation
    {
        public string skillId;
        public string title;
        [TextArea(2, 4)] public string description;
        [TextArea(2, 4)] public string tacticalHint;
    }

    // This asset owns words and timing only. Costs, damage and cooldowns remain authoritative rules.
    [CreateAssetMenu(menuName = "一笔江湖/战斗/表现配置")]
    public sealed class BattlePresentationProfile : ScriptableObject
    {
        public TechniquePresentation[] techniques = DefaultTechniques();
        [Min(.05f)] public float actionDuration = .9f;
        [Min(0)] public float npcThinkDelay = 1.2f;
        [Min(0)] public float impactDelay = .28f;
        [Min(.1f)] public float floatDuration = 1.2f;
        [Min(0)] public float victoryDelay = .55f;

        public TechniquePresentation Find(string id)
        {
            if (techniques != null) foreach (var entry in techniques)
                if (entry != null && entry.skillId == id) return entry;
            return null;
        }

        public string[] ValidateConfiguration()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            if (techniques == null) errors.Add("技能文案列表为空。");
            else for (int i = 0; i < techniques.Length; i++)
            {
                var entry = techniques[i];
                if (entry == null) { errors.Add("技能文案第 " + (i + 1) + " 项为空。"); continue; }
                if (Array.IndexOf(BattleContent.Ids, entry.skillId) < 0 && entry.skillId != "basic" && entry.skillId != "rest") errors.Add("未知技能 ID：" + entry.skillId);
                if (!ids.Add(entry.skillId ?? "")) errors.Add("重复技能 ID：" + entry.skillId);
            }
            CheckDuration(errors, "行动时长", actionDuration, .05f);
            CheckDuration(errors, "敌人思考间隔", npcThinkDelay, 0);
            CheckDuration(errors, "命中延迟", impactDelay, 0);
            CheckDuration(errors, "飘字时长", floatDuration, .1f);
            CheckDuration(errors, "胜负动作延迟", victoryDelay, 0);
            return errors.ToArray();
        }

        static void CheckDuration(List<string> errors, string label, float value, float minimum)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum) errors.Add(label + "必须是有限数值，且不小于 " + minimum + " 秒。"); }

        public static TechniquePresentation[] DefaultTechniques()
        {
            return new[] {
                Entry("dianxue", "点穴指", "8 基础伤害，留下破绽。", "先点穴，再于下个个人回合接裂石；留足 3 内力。"),
                Entry("lieshi", "裂石掌", "18 基础伤害；命中破绽额外增加 8 基础伤害。", "抓住对手破绽；倍率作用于合计伤害，再由护盾吸收。"),
                Entry("huifeng", "回风诀", "10 基础护盾，获得蓄风。", "回风接流火附加灼伤；也能缓冲下一次进攻。"),
                Entry("liuhuo", "流火诀", "16 基础伤害；消耗蓄风，附加两次各 3 点灼伤。", "蓄风后出招，灼伤在目标个人回合开始结算。"),
                Entry("huti", "护体功", "16 基础护盾；新护盾与现有护盾取较大值。", "预判进攻时护体；反照心法在护盾承受直接攻击后获得反击。"),
                Entry("qingxin", "清心诀", "回复 10 基础生命；优先清除灼伤，否则清除破绽。", "回复不能超过 100 生命；已有灼伤时，清除它不会同时解除破绽。"),
                Entry("basic", "吐纳掌", "无需画线，不耗内力，造成 6 点伤害。", "内力紧张时稳定进攻；有效反照仍可追加伤害。"),
                Entry("rest", "调息", "回复 2 内力，最多 6 点。", "守拙心法主动调息附带 4 护盾；超时调息没有这项护盾。")
            };
        }

        static TechniquePresentation Entry(string id, string title, string description, string hint)
        {
            return new TechniquePresentation { skillId = id, title = title, description = description, tacticalHint = hint };
        }
    }
}
