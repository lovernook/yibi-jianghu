using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Yibi.UI
{
    /// <summary>Saved modal scope: isolates keyboard navigation and restores a still usable selection.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ModalFocusScope : MonoBehaviour
    {
        public Selectable defaultSelection;
        [Tooltip("The shared HUD group outside modal content. Its original interaction state is restored when the last modal closes.")]
        public CanvasGroup backgroundGroup;

        private static readonly List<ModalFocusScope> active = new List<ModalFocusScope>();
        private static readonly Dictionary<CanvasGroup, bool> originalBackgroundStates = new Dictionary<CanvasGroup, bool>();
        private CanvasGroup ownGroup;
        private GameObject previousSelection;
        private Transform externalPanel;
        private bool originalIgnoreParentGroups, originalInteractable;
        private bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active.Clear(); originalBackgroundStates.Clear(); }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            registered = true;
            ownGroup = GetComponent<CanvasGroup>();
            originalIgnoreParentGroups = ownGroup.ignoreParentGroups;
            originalInteractable = ownGroup.interactable;
            ownGroup.ignoreParentGroups = true;
            previousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (backgroundGroup != null && backgroundGroup != ownGroup && !originalBackgroundStates.ContainsKey(backgroundGroup))
                originalBackgroundStates.Add(backgroundGroup, backgroundGroup.interactable);
            active.Remove(this);
            active.Add(this);
            ApplyInteractionScopes();
            var top = Top();
            if (top != null) top.EnsureSelection();
        }

        private void OnDisable()
        {
            if (!registered) return;
            registered = false;
            active.Remove(this);
            if (ownGroup != null) {
                ownGroup.ignoreParentGroups = originalIgnoreParentGroups;
                ownGroup.interactable = originalInteractable;
            }
            if (!ReferenceEquals(backgroundGroup, null) && !UsesBackground(backgroundGroup)) {
                bool original;
                if (originalBackgroundStates.TryGetValue(backgroundGroup, out original)) {
                    if (backgroundGroup != null) backgroundGroup.interactable = original;
                    originalBackgroundStates.Remove(backgroundGroup);
                }
            }
            ApplyInteractionScopes();
            var events = EventSystem.current;
            if (events == null) return;
            if (RememberHigherExternal(events.currentSelectedGameObject)) return;
            var top = Top();
            if (IsUsable(previousSelection) && (top == null || previousSelection.transform.IsChildOf(top.transform)))
                events.SetSelectedGameObject(previousSelection);
            else if (top != null) top.SelectDefault();
            else events.SetSelectedGameObject(null);
            previousSelection = null;
        }

        private void LateUpdate() { if (registered && Top() == this) EnsureSelection(); }

        private static bool UsesBackground(CanvasGroup group)
        {
            foreach (var scope in active) if (scope != null && scope.isActiveAndEnabled && scope.backgroundGroup == group) return true;
            return false;
        }

        private static void ApplyInteractionScopes()
        {
            var top = Top();
            foreach (var scope in active) {
                if (scope == null || !scope.isActiveAndEnabled) continue;
                if (scope.backgroundGroup != null && scope.backgroundGroup != scope.ownGroup)
                    scope.backgroundGroup.interactable = false;
                scope.ownGroup.interactable = scope == top;
            }
        }

        private static ModalFocusScope Top()
        {
            ModalFocusScope result = null;
            int highest = int.MinValue;
            foreach (var scope in active) {
                if (scope == null || !scope.isActiveAndEnabled) continue;
                var layer = scope.GetComponent<ModalLayer>();
                int order = layer != null ? layer.order : 0;
                if (order >= highest) { result = scope; highest = order; }
            }
            return result;
        }

        private void EnsureSelection()
        {
            var events = EventSystem.current;
            if (events == null) return;
            var selection = events.currentSelectedGameObject;
            if (RememberHigherExternal(selection)) return;
            if (externalPanel != null && externalPanel.gameObject.activeInHierarchy) return;
            externalPanel = null;
            if (!IsUsable(selection) || !selection.transform.IsChildOf(transform)) SelectDefault();
        }

        private void SelectDefault()
        {
            var events = EventSystem.current;
            if (events == null) return;
            if (RememberHigherExternal(events.currentSelectedGameObject)) return;
            events.SetSelectedGameObject(null);
            if (defaultSelection != null && defaultSelection.transform.IsChildOf(transform) && IsUsable(defaultSelection.gameObject)) {
                events.SetSelectedGameObject(defaultSelection.gameObject);
                return;
            }
            foreach (var control in GetComponentsInChildren<Selectable>())
                if (IsUsable(control.gameObject)) { events.SetSelectedGameObject(control.gameObject); return; }
        }

        private bool RememberHigherExternal(GameObject selection)
        {
            if (!IsUsable(selection) || selection.transform.IsChildOf(transform)) return false;
            var canvas = selection.GetComponentInParent<Canvas>();
            var ownCanvas = GetComponent<Canvas>();
            if (canvas == null || ownCanvas == null || canvas.sortingOrder <= ownCanvas.sortingOrder) return false;
            var root = selection.transform;
            while (root.parent != null && root.parent != canvas.transform) root = root.parent;
            externalPanel = root;
            return true;
        }

        private static bool IsUsable(GameObject value)
        {
            if (value == null || !value.activeInHierarchy) return false;
            var selectable = value.GetComponent<Selectable>();
            return selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }
    }
}
