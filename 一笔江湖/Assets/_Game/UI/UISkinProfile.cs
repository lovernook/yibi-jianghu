using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yibi.UI
{
    /// <summary>Authored visual assets only. No navigation, game rules or input ownership.</summary>
    [CreateAssetMenu(menuName = "一笔江湖/UI/界面外观", fileName = "UISkinProfile")]
    public sealed class UISkinProfile : ScriptableObject
    {
        [Header("购买资源中的独立切图")]
        public Sprite paperPanel, darkPanel, headerPattern, button, frame, questIcon;
        [Tooltip("依次对应点穴、裂石、回风、流火、护体、清心；只决定外观。")]
        public Sprite[] techniqueIcons = new Sprite[6];
        [Header("色板")]
        public Color darkTint = Color.white;
        public Color paperTint = new Color(1f, .97f, .88f, 1f);
        public Color buttonTint = new Color(.19f, .28f, .30f, 1f);
        public Color primaryTint = new Color(.72f, .55f, .30f, 1f);
        public Color frameTint = new Color(.80f, .65f, .38f, 1f);
        public Color lightText = new Color(.97f, .94f, .83f, 1f);
        public Color inkText = new Color(.10f, .16f, .17f, 1f);
        public Color mutedInkText = new Color(.28f, .32f, .31f, 1f);
        public Color titleInkText = new Color(.32f, .20f, .09f, 1f);
        public Color mutedLightText = new Color(.72f, .80f, .78f, 1f);
        public Color highlighted = new Color(1f, .94f, .77f, 1f);
        public Color pressed = new Color(.69f, .78f, .77f, 1f);
        public Color disabled = new Color(.46f, .49f, .48f, 1f);
        public Color lockedCard = new Color(.15f, .17f, .18f, 1f);
        public Color normalCard = new Color(.23f, .31f, .32f, 1f);
        public Color selectedCard = new Color(.48f, .37f, .20f, 1f);

        public string[] ValidateConfiguration()
        {
            var errors = new List<string>();
            if (paperPanel == null || darkPanel == null || headerPattern == null || button == null || frame == null || questIcon == null)
                errors.Add("纸面、暗纹、标题、按钮、边框和任务图标必须全部指定。");
            if (techniqueIcons == null || techniqueIcons.Length != 6) errors.Add("需要六个秘籍图标。");
            else for (int i = 0; i < techniqueIcons.Length; i++) if (techniqueIcons[i] == null) errors.Add("秘籍图标缺失：" + i);
            return errors.ToArray();
        }
    }
}
