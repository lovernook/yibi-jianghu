using System;
using UnityEngine;
using UnityEngine.UI;

namespace Yibi.Presentation
{
    [Serializable]
    public sealed class WorkshopPreviewOption
    {
        public string label;
        [Tooltip("空值表示移动混合树；非空值匹配动作配置的 ID。")]
        public string actionId;
        [Min(0)] public float movementSpeed;
    }

    public sealed class AnimationWorkshop : MonoBehaviour
    {
        public CharacterMotion[] actors;
        public Text state;
        [TextArea] public string candidateNote;
        public WorkshopPreviewOption[] previews = {
            new WorkshopPreviewOption {label = "待机"},
            new WorkshopPreviewOption {label = "行走", movementSpeed = 3.5f},
            new WorkshopPreviewOption {label = "奔跑", movementSpeed = 6},
            new WorkshopPreviewOption {label = "出掌", actionId = "Attack"},
            new WorkshopPreviewOption {label = "运功", actionId = "Cast"},
            new WorkshopPreviewOption {label = "受击", actionId = "Hit"},
            new WorkshopPreviewOption {label = "胜利", actionId = "Victory"},
            new WorkshopPreviewOption {label = "倒地", actionId = "Defeat"}
        };

        public void Preview(int index)
        {
            if (previews == null || index < 0 || index >= previews.Length || previews[index] == null) return;
            var option = previews[index];
            bool movement = string.IsNullOrEmpty(option.actionId);
            int unavailable = 0;
            if (actors != null) foreach (var actor in actors) {
                if (actor == null) continue;
                actor.ResetPose(); actor.previewMovement = movement; actor.previewSpeed = option.movementSpeed;
                if (!movement && !actor.TryPlayAction(option.actionId)) unavailable++;
            }
            if (state != null) state.text = "动作预览 · " + option.label + "    |    动作配置 / Animation 可编辑" +
                (unavailable == 0 ? "" : "\n" + unavailable + " 个角色未配置此动作，保留待机") +
                (string.IsNullOrEmpty(candidateNote) ? "" : "\n" + candidateNote);
        }
    }
}
