using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yibi.App;
using Yibi.Battle;

namespace Yibi.World
{
    /// <summary>The fixed panel and row template are authored in Unity; only variable content is cloned.</summary>
    public sealed class QuestJournalPresenter : MonoBehaviour
    {
        public ValleyPresenter valley;
        public GameObject panel;
        public RectTransform content;
        public QuestRowView rowPrefab;
        public Text summary;
        private readonly List<QuestRowView> rows = new List<QuestRowView>();
        public bool IsOpen { get { return panel != null && panel.activeSelf; } }

        private void OnEnable() { ProfileStore.Changed += OnProfileChanged; }
        private void OnDisable() { ProfileStore.Changed -= OnProfileChanged; }
        private void OnProfileChanged() { if (IsOpen) Refresh(); }

        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (GameNavigation.IsInputBlocked) return;
            if (valley.journey != null && valley.journey.IsModal) valley.journey.CloseDialogue();
            valley.inventoryPanel.SetActive(false);
            valley.pausePanel.SetActive(false);
            panel.SetActive(true);
            valley.explorer.inputLocked = true;
            Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
            if (valley != null && valley.explorer != null)
                valley.explorer.inputLocked = GameNavigation.IsInputBlocked || valley.inventoryPanel.activeSelf ||
                    valley.pausePanel.activeSelf || (valley.journey != null && valley.journey.IsModal);
        }

        public void Refresh()
        {
            if (ProfileStore.Current == null || ProfileStore.Progress == null) return;
            var quests = ProfileStore.Progress.GetSnapshots();
            while (rows.Count < quests.Count)
            {
                var row = Instantiate(rowPrefab, content, false);
                row.name = "任务条目 · " + (rows.Count + 1);
                rows.Add(row);
            }
            int completed = 0, ready = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                bool visible = i < quests.Count;
                rows[i].gameObject.SetActive(visible);
                if (!visible) continue;
                rows[i].Bind(quests[i], Claim);
                if (quests[i].State == Yibi.Rules.QuestState.Claimed) completed++;
                if (quests[i].State == Yibi.Rules.QuestState.Ready) ready++;
            }
            summary.text = "已完成 " + completed + " / " + quests.Count + "    ·    可领取 " + ready;
        }

        private void Claim(string id)
        {
            bool claimed = ProfileStore.ClaimQuest(id);
            if (claimed) valley.SetNotice("奖励已放入行囊。");
            Refresh();
            if (!claimed && !string.IsNullOrEmpty(ProfileStore.Notice)) summary.text = ProfileStore.Notice;
        }
    }
}
