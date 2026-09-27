using UnityEngine;
using Yibi.Rules;

namespace Yibi.Progression
{
    [CreateAssetMenu(menuName = "一笔江湖/成长/任务定义", fileName = "Quest")]
    public sealed class QuestDefinitionSO : ScriptableObject
    {
        public string stableId;
        public string title;
        [TextArea(3, 8)] public string description;
        public string targetId;
        [Tooltip("旧存档已领取成就ID。修改此字段前必须准备存档迁移。")]
        public string legacyAchievementId;
        public int order;
        public bool isMain = true;
        public bool autoClaim = true;
        public QuestDefinitionSO[] prerequisites = new QuestDefinitionSO[0];
        public QuestObjective[] objectives = new QuestObjective[0];
        public RewardDefinitionSO reward;

        internal QuestDefinition Build()
        {
            var required = new string[prerequisites.Length];
            for (int i = 0; i < required.Length; i++) required[i] = prerequisites[i].stableId;
            var goals = new QuestObjective[objectives.Length];
            for (int i = 0; i < goals.Length; i++) goals[i] = objectives[i].Copy();
            return new QuestDefinition { Id = stableId, Title = title, Description = description, TargetId = targetId,
                LegacyAchievementId = legacyAchievementId, Order = order, IsMain = isMain, AutoClaim = autoClaim,
                Prerequisites = required, Objectives = goals, Reward = reward == null ? new QuestReward() : reward.Build() };
        }
    }
}
