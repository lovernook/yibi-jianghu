using System;
using System.Collections.Generic;
using System.Text;

namespace Yibi.Rules
{
    public static class QuestRules
    {
        public static QuestState State(QuestDefinition quest, QuestProgressState progress)
        {
            if (progress.ClaimedQuestIds.Contains(quest.Id)) return QuestState.Claimed;
            foreach (var id in quest.Prerequisites)
                if (!progress.ClaimedQuestIds.Contains(id)) return QuestState.Locked;
            foreach (var objective in quest.Objectives)
                if (progress.Fact(objective.factKey) < objective.requiredValue) return QuestState.Active;
            return QuestState.Ready;
        }

        public static QuestSnapshot Snapshot(QuestDefinition quest, QuestProgressState progress)
        {
            var state = State(quest, progress);
            var text = new StringBuilder();
            if (state == QuestState.Claimed) text.Append("已领取");
            else if (quest.Objectives.Length == 0) text.Append(state == QuestState.Locked ? "完成前置任务后领取" : "已完成所有前置任务");
            else
            {
                foreach (var objective in quest.Objectives)
                {
                    if (text.Length > 0) text.Append("\n");
                    text.Append(string.IsNullOrEmpty(objective.label) ? objective.factKey : objective.label);
                    text.Append(" ").Append(Math.Min(progress.Fact(objective.factKey), objective.requiredValue));
                    text.Append(" / ").Append(objective.requiredValue);
                }
                if (state == QuestState.Locked) text.Append("\n前置未完成 · 已记录进度");
            }
            return new QuestSnapshot(quest, state, text.ToString(), RewardText(quest.Reward));
        }

        public static string RewardText(QuestReward reward)
        {
            var parts = new List<string>();
            if (reward.Tickets > 0) parts.Add(reward.Tickets + " 签令");
            if (reward.Fragments > 0) parts.Add(reward.Fragments + " 碎片");
            foreach (var id in reward.UnlockTechniqueIds) parts.Add("秘籍 · " + BattleContent.Name(id));
            return parts.Count == 0 ? "江湖阅历" : string.Join(" · ", parts.ToArray());
        }
    }
}
