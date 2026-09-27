using UnityEngine;

namespace Yibi.World
{
    [CreateAssetMenu(menuName="一笔江湖/内容/对话",fileName="Dialogue")]
    public sealed class DialogueDefinitionSO : ScriptableObject
    {
        public string speaker;
        [TextArea(4,12)] public string body;
        public string acceptLabel="继续";
        [TextArea(2,6)] public string blockedBody="请先完成相关任务，再来交谈。";
        [Tooltip("所有任务均已领取奖励时，开放此对话的行为。空数组表示无前置。")]
        public string[] requiredQuestIds=new string[0];
    }
}
