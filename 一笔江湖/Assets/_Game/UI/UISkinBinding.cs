using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Yibi.UI
{
    public enum UISkinImageRole { DarkPanel, PaperPanel, Header, Button, PrimaryButton, Frame, QuestIcon }
    public enum UISkinTextRole { Light, Ink, MutedInk, TitleInk, MutedLight, Gold, ButtonLabel, PrimaryButtonLabel }

    [Serializable] public sealed class UISkinImageSlot { public Image target; public UISkinImageRole role; }
    [Serializable] public sealed class UISkinTextSlot { public Text target; public UISkinTextRole role; }

    /// <summary>
    /// Saved references to existing UI. Apply is explicit, so gameplay-owned highlights and status
    /// colours are never overwritten each frame or when a modal opens. No objects are constructed here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UISkinBinding : MonoBehaviour
    {
        public UISkinProfile profile;
        public UISkinImageSlot[] images = new UISkinImageSlot[0];
        public UISkinTextSlot[] texts = new UISkinTextSlot[0];

        [ContextMenu("应用外观配置到已保存的界面")]
        public void Apply()
        {
            if (profile == null) return;
            foreach (var slot in images) {
                if (slot == null || slot.target == null) continue;
                var image = slot.target;
                image.type = Image.Type.Sliced;
                // The purchased border contains opaque black centre pixels. Render only its
                // nine-slice edges so the panel/button below retains its tint and state feedback.
                image.fillCenter = slot.role != UISkinImageRole.Frame;
                image.preserveAspect = false;
                switch (slot.role) {
                    case UISkinImageRole.PaperPanel: image.sprite = profile.paperPanel; image.color = profile.paperTint; break;
                    case UISkinImageRole.Header: image.sprite = profile.headerPattern; image.color = Color.white; break;
                    case UISkinImageRole.Button: image.sprite = profile.button; image.color = profile.buttonTint; break;
                    case UISkinImageRole.PrimaryButton: image.sprite = profile.button; image.color = profile.primaryTint; break;
                    case UISkinImageRole.Frame: image.sprite = profile.frame; image.color = profile.frameTint; break;
                    case UISkinImageRole.QuestIcon:
                        image.sprite = profile.questIcon; image.color = Color.white; image.type = Image.Type.Simple; image.preserveAspect = true; break;
                    default: image.sprite = profile.darkPanel; image.color = profile.darkTint; break;
                }
                var button = image.GetComponent<Button>();
                if (button != null) {
                    var colours = button.colors; colours.normalColor = Color.white;
                    colours.highlightedColor = profile.highlighted; colours.pressedColor = profile.pressed;
                    colours.selectedColor = profile.highlighted; colours.disabledColor = profile.disabled; button.colors = colours;
                }
            }
            foreach (var slot in texts) {
                if (slot == null || slot.target == null) continue;
                switch (slot.role) {
                    case UISkinTextRole.Ink: slot.target.color = profile.inkText; break;
                    case UISkinTextRole.MutedInk: slot.target.color = profile.mutedInkText; break;
                    case UISkinTextRole.TitleInk: slot.target.color = profile.titleInkText; break;
                    case UISkinTextRole.MutedLight: slot.target.color = profile.mutedLightText; break;
                    case UISkinTextRole.Gold: slot.target.color = profile.frameTint; break;
                    // Both sourced button backgrounds become dark in the disabled state. Keep
                    // their labels bright independently of the surrounding paper surface.
                    case UISkinTextRole.ButtonLabel:
                    case UISkinTextRole.PrimaryButtonLabel: slot.target.color = profile.lightText; break;
                    default: slot.target.color = profile.lightText; break;
                }
            }
        }

        /// <summary>Checks authored sprite bindings without comparing gameplay-owned state colours.</summary>
        public string[] ValidateApplied()
        {
            var errors = new List<string>();
            if (profile == null) { errors.Add("外观配置引用失效。"); return errors.ToArray(); }
            if (images == null) { errors.Add("外观图像绑定缺失。"); return errors.ToArray(); }
            for (int i = 0; i < images.Length; i++) {
                var slot = images[i];
                if (slot == null || slot.target == null) { errors.Add("外观图像目标缺失：" + i); continue; }
                Sprite expected;
                switch (slot.role) {
                    case UISkinImageRole.PaperPanel: expected = profile.paperPanel; break;
                    case UISkinImageRole.Header: expected = profile.headerPattern; break;
                    case UISkinImageRole.Button: case UISkinImageRole.PrimaryButton: expected = profile.button; break;
                    case UISkinImageRole.Frame: expected = profile.frame; break;
                    case UISkinImageRole.QuestIcon: expected = profile.questIcon; break;
                    default: expected = profile.darkPanel; break;
                }
                if (expected == null || slot.target.sprite != expected) errors.Add("外观图像未应用：" + slot.target.name);
                if (slot.role == UISkinImageRole.Frame && slot.target.fillCenter) errors.Add("边框遮挡中心：" + slot.target.name);
            }
            if (texts != null) foreach (var slot in texts) {
                if (slot == null || slot.target == null) continue;
                if ((slot.role == UISkinTextRole.ButtonLabel || slot.role == UISkinTextRole.PrimaryButtonLabel) && slot.target.color != profile.lightText)
                    errors.Add("按钮文字对比色未应用：" + slot.target.name);
            }
            return errors.ToArray();
        }
    }
}
