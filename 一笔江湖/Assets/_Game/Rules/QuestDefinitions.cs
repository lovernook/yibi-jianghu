using System;
using System.Collections.Generic;

namespace Yibi.Rules
{
    [Serializable]
    public sealed class QuestObjective
    {
        public string factKey;
        public int requiredValue = 1;
        public string label;
        public QuestObjective Copy() { return new QuestObjective { factKey = factKey, requiredValue = requiredValue, label = label }; }
    }

    public sealed class QuestReward
    {
        public string Id;
        public int Tickets, Fragments;
        public string[] UnlockTechniqueIds = new string[0];
    }

    public sealed class QuestDefinition
    {
        public string Id, Title, Description, TargetId, LegacyAchievementId;
        public int Order;
        public bool IsMain, AutoClaim;
        public string[] Prerequisites = new string[0];
        public QuestObjective[] Objectives = new QuestObjective[0];
        public QuestReward Reward = new QuestReward();
    }

    [Serializable]
    public sealed class LegacyFactMapping
    {
        public string factKey;
        public string[] achievementIds = new string[0];
        public LegacyFactMapping Copy()
        {
            return new LegacyFactMapping { factKey = factKey, achievementIds = (string[])achievementIds.Clone() };
        }
    }

    [Serializable]
    public sealed class QuestFactRecord
    {
        public string key;
        public int value;
        public QuestFactRecord Copy() { return new QuestFactRecord { key = key, value = value }; }
    }

    // This state contains only progression data. The repository owns currency and save transactions.
    public sealed class QuestProgressState
    {
        public readonly List<QuestFactRecord> Facts = new List<QuestFactRecord>();
        public readonly List<string> ClaimedQuestIds = new List<string>();
        public readonly List<string> LegacyAchievements = new List<string>();

        public QuestProgressState Copy()
        {
            var copy = new QuestProgressState();
            foreach (var fact in Facts) copy.Facts.Add(fact.Copy());
            copy.ClaimedQuestIds.AddRange(ClaimedQuestIds);
            copy.LegacyAchievements.AddRange(LegacyAchievements);
            return copy;
        }

        public int Fact(string key)
        {
            foreach (var fact in Facts) if (fact.key == key) return fact.value;
            return 0;
        }

        public bool SetMaximum(string key, int value)
        {
            foreach (var fact in Facts)
            {
                if (fact.key != key) continue;
                if (fact.value >= value) return false;
                fact.value = value;
                return true;
            }
            if (value == 0) return false;
            Facts.Add(new QuestFactRecord { key = key, value = value });
            return true;
        }
    }

    public enum QuestState { Locked, Active, Ready, Claimed }

    public sealed class QuestSnapshot
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string TargetId { get; private set; }
        public QuestState State { get; private set; }
        public string ProgressText { get; private set; }
        public string RewardText { get; private set; }
        public bool IsMain { get; private set; }

        public QuestSnapshot(QuestDefinition definition, QuestState state, string progress, string reward)
        {
            Id = definition.Id; Title = definition.Title; Description = definition.Description;
            TargetId = definition.TargetId; IsMain = definition.IsMain; State = state;
            ProgressText = progress; RewardText = reward;
        }
    }
}
