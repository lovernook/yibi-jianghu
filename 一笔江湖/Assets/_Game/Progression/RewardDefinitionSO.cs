using UnityEngine;
using Yibi.Rules;

namespace Yibi.Progression
{
    [CreateAssetMenu(menuName = "一笔江湖/成长/奖励定义", fileName = "Reward")]
    public sealed class RewardDefinitionSO : ScriptableObject
    {
        public string stableId;
        [Min(0)] public int tickets;
        [Min(0)] public int fragments;
        public string[] unlockTechniqueIds = new string[0];

        internal QuestReward Build()
        {
            return new QuestReward { Id = stableId, Tickets = tickets, Fragments = fragments,
                UnlockTechniqueIds = unlockTechniqueIds == null ? new string[0] : (string[])unlockTechniqueIds.Clone() };
        }
    }
}
