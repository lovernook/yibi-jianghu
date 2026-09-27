using System.Collections.Generic;
using UnityEngine;

namespace Yibi.Presentation
{
    /// <summary>Plays saved animation assets. Gameplay state never depends on animation events.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterMotion : MonoBehaviour
    {
        public Animator animator;
        public CharacterMotionProfile profile;
        [Range(0,8)] public float previewSpeed;
        public bool previewMovement;
        private float actionUntil;
        private bool terminal;
        private static readonly CharacterMotionAction[] legacyActions = CharacterMotionProfile.CreateDefaultActions();
        private bool actionActive;
        private RuntimeAnimatorController parameterController;
        private string cachedParameter;
        private bool speedParameterExists;
        public string CurrentAction { get; private set; } = "Locomotion";
        private void Awake(){if(animator==null)animator=GetComponent<Animator>();}
        private void Update()
        {
            if(previewMovement)SetSpeed(previewSpeed);
            if(!terminal&&actionActive&&Time.unscaledTime>=actionUntil){actionActive=false;ReturnToLocomotion(false);}
        }
        public void SetSpeed(float speed)
        {
            if (animator == null || !CharacterMotionProfile.Finite(speed)) return;
            string parameter = profile == null ? "Speed" : profile.speedParameter;
            if (string.IsNullOrEmpty(parameter) || !HasFloatParameter(parameter)) return;
            float maximum = profile == null || !CharacterMotionProfile.Finite(profile.maximumSpeed) ? 8 : Mathf.Max(.01f, profile.maximumSpeed);
            float damping = profile == null || !CharacterMotionProfile.Finite(profile.speedDampingSeconds) ? .12f : Mathf.Max(0, profile.speedDampingSeconds);
            animator.SetFloat(Animator.StringToHash(parameter), Mathf.Clamp(speed, 0, maximum), damping, Time.unscaledDeltaTime);
        }
        public void PlayAction(string name)
        {
            TryPlayAction(name);
        }
        public bool TryPlayAction(string name)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return false;
            CharacterMotionAction action = null;
            if (profile != null) profile.TryGet(name, out action);
            else foreach (var item in legacyActions) if (item.id == name) { action = item; break; }
            if (terminal && (action == null || !action.holdUntilReset)) return false;
            if (action == null || !CharacterMotionProfile.Finite(action.durationSeconds) || action.durationSeconds <= 0 ||
                !CharacterMotionProfile.Finite(action.transitionSeconds) || action.transitionSeconds < 0 || !HasState(action.stateName)) {
                // Missing optional action never sends an invalid state to Animator or locks gameplay.
                if (terminal) return false;
                actionActive = false;
                ReturnToLocomotion(false);
                return false;
            }
            CurrentAction = name;
            terminal = action.holdUntilReset;
            actionActive = true;
            actionUntil = Time.unscaledTime + action.durationSeconds;
            animator.CrossFadeInFixedTime(action.stateName, action.transitionSeconds, 0, 0);
            return true;
        }

        public void ResetPose()
        {
            terminal = false; actionActive = false; actionUntil = 0;
            string parameter = profile == null ? "Speed" : profile.speedParameter;
            if (animator != null && !string.IsNullOrEmpty(parameter) && HasFloatParameter(parameter)) animator.SetFloat(Animator.StringToHash(parameter), 0);
            ReturnToLocomotion(true);
        }

        private void ReturnToLocomotion(bool immediate)
        {
            CurrentAction = "Locomotion";
            string state = profile == null ? "Locomotion" : profile.locomotionState;
            if (animator == null || !animator.isActiveAndEnabled || !HasState(state)) return;
            if (immediate) animator.Play(state, 0, 0);
            else animator.CrossFadeInFixedTime(state, profile == null || !CharacterMotionProfile.Finite(profile.returnTransitionSeconds) ? .16f : Mathf.Max(0, profile.returnTransitionSeconds));
        }

        private bool HasState(string state) { return animator != null && animator.runtimeAnimatorController != null && !string.IsNullOrEmpty(state) && animator.HasState(0, Animator.StringToHash(state)); }
        private bool HasFloatParameter(string name)
        {
            if (animator.runtimeAnimatorController == null) return false;
            if (parameterController != animator.runtimeAnimatorController || cachedParameter != name) {
                parameterController = animator.runtimeAnimatorController; cachedParameter = name; speedParameterExists = false;
                foreach (var parameter in animator.parameters)
                    if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Float) { speedParameterExists = true; break; }
            }
            return speedParameterExists;
        }

        public string[] ValidateConfiguration()
        {
            var errors = new List<string>();
            if (profile != null) errors.AddRange(profile.ValidateConfiguration());
            if (animator == null || animator.runtimeAnimatorController == null) { errors.Add("缺少 Animator 或 Controller。"); return errors.ToArray(); }
            if (!animator.isInitialized) return errors.ToArray(); // Editor inspector validates the controller asset before Play.
            string locomotion = profile == null ? "Locomotion" : profile.locomotionState;
            if (!HasState(locomotion)) errors.Add("Controller 缺少移动状态：" + locomotion);
            var actions = profile == null ? legacyActions : profile.actions;
            if (actions != null) foreach (var action in actions) if (action != null && !HasState(action.stateName)) errors.Add("Controller 缺少动作状态：" + action.stateName);
            string speed = profile == null ? "Speed" : profile.speedParameter;
            if (!string.IsNullOrEmpty(speed) && !HasFloatParameter(speed)) errors.Add("Controller 缺少 Float 速度参数：" + speed);
            return errors.ToArray();
        }
    }
}
