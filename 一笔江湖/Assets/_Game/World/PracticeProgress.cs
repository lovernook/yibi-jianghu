using UnityEngine;
using UnityEngine.UI;
using Yibi.UI;
using Yibi.Rules;
using Yibi.Battle;
namespace Yibi.World
{
    public sealed class PracticeProgress:MonoBehaviour
    {
        public GestureBoard board;
        public Text objective;
        public string questId="guiyun.practice";
        private void OnEnable(){ProfileStore.Changed+=Refresh;}
        private void OnDisable(){ProfileStore.Changed-=Refresh;}
        private void Start(){ProfileStore.Load();Refresh();}
        private void Refresh()
        {
            if(objective==null||ProfileStore.Current==null)return;
            foreach(var quest in ProfileStore.Progress.GetSnapshots())
            {
                if(quest.Id!=questId)continue;
                objective.text=quest.Title+" · "+quest.ProgressText+"\n奖励 · "+quest.RewardText+
                    (quest.State==QuestState.Claimed?" · 已领取":quest.State==QuestState.Ready?" · 可在任务日志领取":"");
                return;
            }
            objective.text="自由练功 · 手绘记录会保存在本机";
        }
    }
}
