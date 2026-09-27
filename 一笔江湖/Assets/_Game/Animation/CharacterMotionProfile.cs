using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yibi.Presentation
{
    [Serializable]
    public sealed class CharacterMotionAction
    {
        public string id;
        public string stateName;
        [Min(0)] public float transitionSeconds = .09f;
        [Min(.01f)] public float durationSeconds = 1.15f;
        [Tooltip("胜负等姿态保持到 ResetPose，不自动回到移动。")]
        public bool holdUntilReset;
    }

    /// <summary>Presentation timing only. No animation event decides battle damage or rewards.</summary>
    [CreateAssetMenu(menuName = "一笔江湖/表现/角色动作配置")]
    public sealed class CharacterMotionProfile : ScriptableObject
    {
        public string locomotionState = "Locomotion";
        public string speedParameter = "Speed";
        [Min(0)] public float returnTransitionSeconds = .16f;
        [Min(0)] public float speedDampingSeconds = .12f;
        [Min(.01f)] public float maximumSpeed = 8;
        public CharacterMotionAction[] actions = CreateDefaultActions();

        public static CharacterMotionAction[] CreateDefaultActions()
        {
            return new[] {
                Action("Attack", .86f), Action("Cast", 1.15f), Action("Hit", .42f),
                Action("Victory", 1.15f, true), Action("Defeat", 1.15f, true)
            };
        }

        private static CharacterMotionAction Action(string id, float seconds, bool hold = false)
        {
            return new CharacterMotionAction {id = id, stateName = id, durationSeconds = seconds, holdUntilReset = hold};
        }

        public bool TryGet(string id, out CharacterMotionAction result)
        {
            if (actions != null)
                foreach (var action in actions)
                    if (action != null && string.Equals(action.id, id, StringComparison.Ordinal)) { result = action; return true; }
            result = null;
            return false;
        }

        public string[] ValidateConfiguration()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(locomotionState)) errors.Add("移动状态名称不能为空。");
            if (!Finite(returnTransitionSeconds) || returnTransitionSeconds < 0) errors.Add("返回过渡时间必须为非负有限数。");
            if (!Finite(speedDampingSeconds) || speedDampingSeconds < 0) errors.Add("速度阻尼必须为非负有限数。");
            if (!Finite(maximumSpeed) || maximumSpeed <= 0) errors.Add("速度上限必须为正有限数。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (actions == null) return errors.ToArray();
            foreach (var action in actions) {
                if (action == null) { errors.Add("动作配置不能包含空项。"); continue; }
                if (string.IsNullOrWhiteSpace(action.id) || !ids.Add(action.id)) errors.Add("动作 ID 为空或重复：" + action.id);
                if (string.IsNullOrWhiteSpace(action.stateName)) errors.Add("动作状态名称不能为空：" + action.id);
                if (!Finite(action.durationSeconds) || action.durationSeconds <= 0) errors.Add("动作时长必须为正有限数：" + action.id);
                if (!Finite(action.transitionSeconds) || action.transitionSeconds < 0) errors.Add("动作过渡必须为非负有限数：" + action.id);
            }
            return errors.ToArray();
        }

        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
