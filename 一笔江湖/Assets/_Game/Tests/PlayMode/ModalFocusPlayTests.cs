using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yibi.App;
using Yibi.Battle;
using Yibi.UI;
using Yibi.World;

public class ModalFocusPlayTests
{
    private IDisposable profile;

    [SetUp]
    public void SetUp()
    {
        if (GameRoot.Instance != null && GameRoot.Instance.HasError) GameRoot.Instance.DismissError();
        profile = ProfileStore.UseTransientProfile(new PlayerProfile());
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try { yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null; }
        finally { profile.Dispose(); }
    }

    [UnityTest]
    public IEnumerator SavedModalsHaveIndependentFocusAndShareHudBackground()
    {
        yield return SceneManager.LoadSceneAsync("Valley"); yield return null;
        var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
        var group = valley.GetComponent<CanvasGroup>();
        Assert.IsNotNull(group);
        var fontBinding = valley.GetComponent<SavedChineseFont>();
        Assert.IsNotNull(fontBinding.profile, "The font family configuration must be saved.");
        foreach (var label in valley.GetComponentsInChildren<Text>(true)) {
            Assert.IsNotNull(label.font, label.name);
            Assert.IsNotNull(label.font.material, label.name + " has a stale font wrapper without a render material.");
            Assert.IsNotNull(label.font.material.mainTexture, label.name + " has no font atlas.");
        }
        foreach (var panel in new[] {valley.inventoryPanel, valley.pausePanel, valley.journal.panel, valley.journey.dialoguePanel}) {
            var scope = panel.GetComponent<ModalFocusScope>();
            Assert.IsNotNull(scope, panel.name);
            Assert.AreSame(group, scope.backgroundGroup);
            Assert.IsNotNull(scope.defaultSelection);
            Assert.IsTrue(scope.defaultSelection.transform.IsChildOf(panel.transform));
        }
    }

    [UnityTest]
    public IEnumerator OpenJournalBlocksUnderlyingSubmitAndRestoresVisiblePreviousSelection()
    {
        yield return SceneManager.LoadSceneAsync("Valley"); yield return null;
        var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
        var entry = valley.transform.Find("旅程顶栏/任务日志入口").GetComponent<Button>();
        var events = EventSystem.current;
        int invoked = 0; entry.onClick.AddListener(() => invoked++);
        events.SetSelectedGameObject(entry.gameObject);
        valley.journal.Open(); yield return null;
        Assert.IsFalse(entry.IsInteractable(), "The background must reject keyboard submit as well as mouse clicks.");
        Assert.IsTrue(events.currentSelectedGameObject.transform.IsChildOf(valley.journal.panel.transform));
        ExecuteEvents.Execute(entry.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        Assert.AreEqual(0, invoked);
        Assert.IsTrue(valley.journal.IsOpen);
        events.SetSelectedGameObject(entry.gameObject); yield return null;
        Assert.IsTrue(events.currentSelectedGameObject.transform.IsChildOf(valley.journal.panel.transform));
        valley.journal.Close();
        Assert.IsTrue(entry.IsInteractable());
        Assert.AreSame(entry.gameObject, events.currentSelectedGameObject);
    }

    [UnityTest]
    public IEnumerator ClosingTopModalRestoresLowerScopeButNeverAHiddenControl()
    {
        yield return SceneManager.LoadSceneAsync("Valley"); yield return null;
        var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
        var entry = valley.transform.Find("旅程顶栏/任务日志入口").gameObject;
        var events = EventSystem.current;
        events.SetSelectedGameObject(entry);
        valley.journal.Open(); yield return null;
        entry.SetActive(false);
        valley.pausePanel.SetActive(true); yield return null;
        Assert.IsTrue(events.currentSelectedGameObject.transform.IsChildOf(valley.pausePanel.transform));
        Assert.IsFalse(valley.journal.panel.GetComponent<CanvasGroup>().interactable);
        Assert.IsFalse(valley.GetComponent<CanvasGroup>().interactable);
        valley.pausePanel.SetActive(false); yield return null;
        Assert.IsTrue(events.currentSelectedGameObject.transform.IsChildOf(valley.journal.panel.transform));
        Assert.IsTrue(valley.journal.panel.GetComponent<CanvasGroup>().interactable);
        valley.journal.Close();
        Assert.IsNull(events.currentSelectedGameObject, "A hidden previous control must not regain keyboard focus.");
        Assert.IsTrue(valley.GetComponent<CanvasGroup>().interactable);
    }
}
