using System;
using System.Collections.Generic;
using UnityEngine;
using Yibi.Rules;

namespace Yibi.Progression
{
    [CreateAssetMenu(menuName = "一笔江湖/成长/任务目录", fileName = "QuestCatalog")]
    public sealed class QuestCatalogSO : ScriptableObject
    {
        public QuestDefinitionSO[] quests = new QuestDefinitionSO[0];
        [Tooltip("把指定旧成就的已完成数量迁移为历史事实。只补进度，不在迁移时发奖。")]
        public LegacyFactMapping[] legacyFactMappings = new LegacyFactMapping[0];

        public string[] Validate()
        {
            var errors = new List<string>();
            if (quests == null) return new[] { "任务列表为空。" };
            if (legacyFactMappings == null) errors.Add("旧事实迁移列表为 null。");
            else
            {
                var mappedFacts = new HashSet<string>(StringComparer.Ordinal);
                foreach (var mapping in legacyFactMappings)
                {
                    if (mapping == null) { errors.Add("旧事实迁移含空映射。"); continue; }
                    if (string.IsNullOrWhiteSpace(mapping.factKey)) errors.Add("旧事实迁移缺少事实键。");
                    else if (!mappedFacts.Add(mapping.factKey)) errors.Add("重复旧事实迁移键：" + mapping.factKey);
                    if (mapping.achievementIds == null || mapping.achievementIds.Length == 0)
                    {
                        errors.Add(mapping.factKey + " 的旧事实迁移至少需要一个明确成就 ID。"); continue;
                    }
                    var mappedIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var id in mapping.achievementIds)
                        if (string.IsNullOrWhiteSpace(id) || !mappedIds.Add(id)) errors.Add(mapping.factKey + " 含空或重复旧成就 ID：" + id);
                }
            }
            var ids = new Dictionary<string, QuestDefinitionSO>(StringComparer.Ordinal);
            var legacy = new HashSet<string>(StringComparer.Ordinal);
            var rewards = new Dictionary<string, RewardDefinitionSO>(StringComparer.Ordinal);
            foreach (var quest in quests)
            {
                if (quest == null) { errors.Add("任务目录含空引用。"); continue; }
                string tag = string.IsNullOrWhiteSpace(quest.stableId) ? quest.name : quest.stableId;
                if (string.IsNullOrWhiteSpace(quest.stableId)) errors.Add(tag + " 缺少稳定 ID。");
                else if (ids.ContainsKey(quest.stableId)) errors.Add("重复任务 ID：" + quest.stableId);
                else ids.Add(quest.stableId, quest);
                if (string.IsNullOrWhiteSpace(quest.title)) errors.Add(tag + " 缺少任务标题。");
                if (!string.IsNullOrEmpty(quest.legacyAchievementId) && !legacy.Add(quest.legacyAchievementId)) errors.Add("重复旧成就 ID：" + quest.legacyAchievementId);
                if (quest.prerequisites == null || quest.objectives == null) { errors.Add(tag + " 的前置或目标列表为 null。"); continue; }
                if (quest.prerequisites.Length == 0 && quest.objectives.Length == 0) errors.Add(tag + " 至少需要一个目标或前置。");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var goal in quest.objectives)
                {
                    if (goal == null) { errors.Add(tag + " 含空目标。"); continue; }
                    if (string.IsNullOrWhiteSpace(goal.factKey)) errors.Add(tag + " 的目标缺少事实键。");
                    else if (!keys.Add(goal.factKey)) errors.Add(tag + " 重复目标事实键：" + goal.factKey);
                    if (goal.requiredValue <= 0) errors.Add(tag + " 的目标阈值必须大于 0。");
                }
                var reward = quest.reward;
                if (reward == null) continue; // A narrative quest is allowed to have no material reward.
                if (string.IsNullOrWhiteSpace(reward.stableId)) errors.Add(tag + " 的奖励缺少稳定 ID。");
                else if (rewards.ContainsKey(reward.stableId) && rewards[reward.stableId] != reward) errors.Add("重复奖励 ID：" + reward.stableId);
                else rewards[reward.stableId] = reward;
                if (reward.tickets < 0 || reward.fragments < 0) errors.Add(tag + " 奖励不能为负数。");
                if (reward.unlockTechniqueIds == null) { errors.Add(tag + " 的秘籍奖励列表为 null。"); continue; }
                var techniques = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in reward.unlockTechniqueIds)
                    if (Array.IndexOf(BattleContent.Ids, id) < 0 || !techniques.Add(id)) errors.Add(tag + " 含未知或重复秘籍奖励：" + id);
            }
            foreach (var quest in quests)
            {
                if (quest == null || quest.prerequisites == null) continue;
                var required = new HashSet<string>(StringComparer.Ordinal);
                foreach (var dependency in quest.prerequisites)
                {
                    if (dependency == null) { errors.Add(quest.stableId + " 含空前置引用。"); continue; }
                    if (string.IsNullOrWhiteSpace(dependency.stableId) || !ids.ContainsKey(dependency.stableId) || ids[dependency.stableId] != dependency)
                        errors.Add(quest.stableId + " 的前置不在目录中：" + dependency.name);
                    else if (!required.Add(dependency.stableId)) errors.Add(quest.stableId + " 含重复前置：" + dependency.stableId);
                }
            }
            var visited = new HashSet<QuestDefinitionSO>();
            var visiting = new HashSet<QuestDefinitionSO>();
            foreach (var quest in quests) CheckCycle(quest, visited, visiting, errors);
            return errors.ToArray();
        }

        private static void CheckCycle(QuestDefinitionSO quest, HashSet<QuestDefinitionSO> visited, HashSet<QuestDefinitionSO> visiting, List<string> errors)
        {
            if (quest == null || visited.Contains(quest)) return;
            if (!visiting.Add(quest)) { errors.Add("任务前置存在循环：" + quest.stableId); return; }
            if (quest.prerequisites != null) foreach (var dependency in quest.prerequisites) CheckCycle(dependency, visited, visiting, errors);
            visiting.Remove(quest); visited.Add(quest);
        }

        public QuestDefinition[] BuildDefinitions()
        {
            var errors = Validate();
            if (errors.Length > 0) throw new InvalidOperationException("任务目录无效：\n" + string.Join("\n", errors));
            var definitions = new QuestDefinition[quests.Length];
            for (int i = 0; i < definitions.Length; i++) definitions[i] = quests[i].Build();
            Array.Sort(definitions, delegate(QuestDefinition a, QuestDefinition b) { int order = a.Order.CompareTo(b.Order); return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id); });
            return definitions;
        }

        public LegacyFactMapping[] BuildLegacyFactMappings()
        {
            var errors = Validate();
            if (errors.Length > 0) throw new InvalidOperationException("任务目录无效：\n" + string.Join("\n", errors));
            var mappings = new LegacyFactMapping[legacyFactMappings.Length];
            for (int i = 0; i < mappings.Length; i++) mappings[i] = legacyFactMappings[i].Copy();
            return mappings;
        }
    }
}
