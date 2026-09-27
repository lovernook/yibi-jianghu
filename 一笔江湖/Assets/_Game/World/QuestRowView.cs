using System;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.World
{
    /// <summary>A saved row prefab presents a snapshot; rewards remain in the progression service.</summary>
    public sealed class QuestRowView : MonoBehaviour
    {
        public Text title, category, state, description, progress, reward, claimLabel;
        public Button claimButton;
        public Image accent;
        public Color mainColor = new Color(.76f, .61f, .36f);
        public Color sideColor = new Color(.43f, .69f, .63f);
        public Color inactiveColor = new Color(.52f, .59f, .59f);
        public string QuestId { get; private set; }
        private Action<string> claim;

        private void Awake() { claimButton.onClick.AddListener(Claim); }
        private void OnDestroy() { if (claimButton != null) claimButton.onClick.RemoveListener(Claim); }

        public void Bind(QuestSnapshot snapshot, Action<string> onClaim)
        {
            QuestId = snapshot.Id;
            claim = onClaim;
            title.text = snapshot.Title;
            category.text = snapshot.IsMain ? "主线" : "支线";
            description.text = snapshot.Description;
            progress.text = snapshot.ProgressText;
            reward.text = "奖励 · " + snapshot.RewardText;
            state.text = StateLabel(snapshot.State);
            bool ready = snapshot.State == QuestState.Ready;
            claimButton.interactable = ready;
            claimLabel.text = ready ? "领取奖励" : snapshot.State == QuestState.Claimed ? "已领取" : "待完成";
            var color = snapshot.State == QuestState.Locked ? inactiveColor : snapshot.IsMain ? mainColor : sideColor;
            accent.color = color;
            category.color = color;
            state.color = ready ? sideColor : color;
        }

        private void Claim() { if (claimButton.interactable && claim != null) claim(QuestId); }
        private static string StateLabel(QuestState value)
        {
            switch (value)
            {
                case QuestState.Locked: return "尚未解锁";
                case QuestState.Active: return "进行中";
                case QuestState.Ready: return "可领取";
                case QuestState.Claimed: return "已完成";
                default: return "";
            }
        }
    }
}
