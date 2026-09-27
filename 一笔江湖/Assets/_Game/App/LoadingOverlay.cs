using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Yibi.App
{
    /// <summary>Presenter for the loading/error panel saved in GameRoot.prefab.</summary>
    public sealed class LoadingOverlay : MonoBehaviour
    {
        public GameObject panel;
        public Text status;
        public Text progressText;
        public Slider progressSlider;
        public Button retryButton;
        public Button cancelButton;

        private GameRoot root;
        private int shownPercent = -1;
        private string shownStatus;
        private bool wasVisible;
        private bool wasError;
        private bool restoreSelectionPending;
        private GameObject previousSelection;

        public bool IsConfigured => panel != null && status != null && progressText != null
            && progressSlider != null && retryButton != null && cancelButton != null;

        public void Bind(GameRoot owner)
        {
            root = owner;
            // Saved prefabs expose their callbacks in the Inspector. Dynamic
            // binding is only a fallback for a view without persistent listeners.
            retryButton.onClick.RemoveListener(Retry);
            cancelButton.onClick.RemoveListener(Cancel);
            if (retryButton.onClick.GetPersistentEventCount() == 0)
                retryButton.onClick.AddListener(Retry);
            if (cancelButton.onClick.GetPersistentEventCount() == 0)
                cancelButton.onClick.AddListener(Cancel);
            shownPercent = -1;
            shownStatus = null;
            Refresh();
        }

        public void Unbind(GameRoot owner)
        {
            if (root != owner) return;
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            root = null;
        }

        public void Refresh()
        {
            if (root == null || !IsConfigured) return;
            bool visible = root.IsLoading || root.HasError;
            if (panel.activeSelf != visible) panel.SetActive(visible);
            if (!visible)
            {
                RefreshFocus(false, false);
                return;
            }

            if (shownStatus != root.Status)
            {
                shownStatus = root.Status;
                status.text = shownStatus;
            }
            int percent = Mathf.RoundToInt(root.Progress * 100);
            if (percent != shownPercent)
            {
                shownPercent = percent;
                progressText.text = percent + "%";
                progressSlider.SetValueWithoutNotify(root.Progress);
            }
            progressText.gameObject.SetActive(!root.HasError);
            progressSlider.gameObject.SetActive(!root.HasError);
            retryButton.gameObject.SetActive(root.HasError);
            cancelButton.gameObject.SetActive(root.HasError);
            retryButton.interactable = root.CanRetry;
            cancelButton.interactable = root.HasError;
            RefreshFocus(true, root.HasError);
        }

        private void RefreshFocus(bool visible, bool error)
        {
            var eventSystem = EventSystem.current;
            if (visible)
            {
                bool opened = !wasVisible;
                bool enteredError = error && !wasError;
                if (opened)
                {
                    previousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                    restoreSelectionPending = false;
                }
                wasVisible = true;
                wasError = error;
                // OnSelect callbacks may themselves request navigation. Unity
                // forbids another selection while that callback is executing;
                // the following Refresh then enforces the same focus boundary.
                if (eventSystem == null || eventSystem.alreadySelecting) return;

                var selected = eventSystem.currentSelectedGameObject;
                if (!error)
                {
                    if (selected != null) eventSystem.SetSelectedGameObject(null);
                }
                else if (opened || enteredError || selected == null
                    || !selected.transform.IsChildOf(panel.transform))
                {
                    eventSystem.SetSelectedGameObject(cancelButton.gameObject);
                }
                return;
            }

            if (wasVisible) restoreSelectionPending = true;
            wasVisible = wasError = false;
            if (!restoreSelectionPending) return;
            if (previousSelection == null || !previousSelection.activeInHierarchy)
            {
                restoreSelectionPending = false;
                previousSelection = null;
                return;
            }
            var selectable = previousSelection.GetComponent<Selectable>();
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            {
                restoreSelectionPending = false;
                previousSelection = null;
                return;
            }
            if (eventSystem == null || eventSystem.alreadySelecting) return;
            var restore = previousSelection;
            previousSelection = null;
            restoreSelectionPending = false;
            eventSystem.SetSelectedGameObject(restore);
        }

        public void Retry() { if (root != null) root.RetryNavigation(); }
        public void Cancel() { if (root != null) root.DismissError(); }

        private void OnDestroy()
        {
            if (root != null) Unbind(root);
        }
    }
}
