using System;
using System.Collections;
using System.Collections.Generic;
using GameFramework;
using GameFramework.Event;
using GameFramework.Procedure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.App;
using Yibi.Battle;
using Yibi.Networking;
using Yibi.World;

public class AppFlowPlayTests
{
    private const float TimeoutSeconds = 30f;
    private IDisposable profile;

    [UnitySetUp]
    public IEnumerator EnterSavedMainMenu()
    {
        profile = ProfileStore.UseTransientProfile(new PlayerProfile());
        DestroyResidents();
        yield return null;
        yield return LoadLegacyScene("MainMenu");
        yield return WaitForProcedure("ProcedureMainMenu");
        AssertSingleRoot(GameRoot.Instance);
        Assert.IsTrue(GameRoot.Instance.loadingOverlay.IsConfigured,
            "The test requires the saved view, not an in-test replacement UI.");
    }

    [UnityTearDown]
    public IEnumerator ReturnToBootstrapAndRestoreProfile()
    {
        try
        {
            // A failed assertion may leave a Unity load in progress. Finish it
            // before disposing the framework and loading the neutral scene.
            if (GameRoot.Instance != null && GameRoot.Instance.IsLoading)
                yield return WaitFor(() => GameRoot.Instance == null || !GameRoot.Instance.IsLoading,
                    "Pending navigation did not finish during cleanup.");
            DestroyResidents();
            yield return null;
            yield return LoadLegacyScene("Bootstrap");
            DestroyResidents();
            yield return null;
        }
        finally
        {
            profile?.Dispose();
            profile = null;
        }
    }

    [UnityTest]
    public IEnumerator NavigationGatesRepeatedClicksAndRoundTripCompletesOnlyOncePerLoad()
    {
        var root = GameRoot.Instance;
        int before = root.CompletedNavigations;

        var selected = UnityEngine.Object.FindObjectOfType<Yibi.World.MainMenuPresenter>().continueText.GetComponentInParent<UnityEngine.UI.Button>();
        EventSystem.current.SetSelectedGameObject(selected.gameObject);

        Assert.IsTrue(GameNavigation.GoTo(GameScene.Valley));
        Assert.IsNull(EventSystem.current.currentSelectedGameObject,
            "Loading must also prevent keyboard Submit from reaching the outgoing menu.");
        Assert.IsFalse(GameNavigation.GoTo(GameScene.GestureLab),
            "A second click in the same frame must not replace or queue a destination.");
        Assert.AreEqual("Valley", root.TargetScene);
        Assert.IsTrue(root.IsLoading);
        Assert.IsTrue(GameNavigation.IsInputBlocked);
        Assert.IsTrue(root.loadingOverlay.panel.activeInHierarchy);

        yield return WaitForNavigation(root, "Valley", "ProcedureWorld", before + 1);
        AssertSingleRoot(root);
        Assert.IsFalse(GameNavigation.IsInputBlocked);
        Assert.IsFalse(root.loadingOverlay.panel.activeSelf);
        yield return AssertCompletionRemains(root, before + 1);

        Assert.IsTrue(GameNavigation.GoTo(GameScene.MainMenu));
        yield return WaitForNavigation(root, "MainMenu", "ProcedureMainMenu", before + 2);
        AssertSingleRoot(root);
        Assert.IsFalse(GameNavigation.IsInputBlocked);
        Assert.IsFalse(root.loadingOverlay.panel.activeSelf);
        yield return AssertCompletionRemains(root, before + 2);
    }

    [UnityTest]
    public IEnumerator FrameworkEventsCanUnsubscribeAndLegacyLoadKeepsTheOriginalRoot()
    {
        var root = GameRoot.Instance;
        var events = root.Events;
        int before = root.CompletedNavigations;
        int requested = 0, completed = 0;
        string requestedScene = null, completedScene = null;
        EventHandler<GameEventArgs> requestHandler = (sender, args) =>
        {
            Assert.AreSame(root, sender);
            requested++;
            requestedScene = ((NavigationRequestedEventArgs)args).SceneName;
        };
        EventHandler<GameEventArgs> completeHandler = (sender, args) =>
        {
            Assert.AreSame(root, sender);
            completed++;
            // Copy values while the callback owns the pooled event arguments.
            completedScene = ((NavigationCompletedEventArgs)args).SceneName;
        };
        events.Subscribe(NavigationRequestedEventArgs.EventId, requestHandler);
        events.Subscribe(NavigationCompletedEventArgs.EventId, completeHandler);
        try
        {
            Assert.IsTrue(GameNavigation.GoTo(GameScene.GestureLab));
            Assert.AreEqual(1, requested, "Request delivery must acquire the gate synchronously.");
            Assert.AreEqual("GestureLab", requestedScene);
            yield return WaitForNavigation(root, "GestureLab", "ProcedurePractice", before + 1);
            yield return WaitFor(() => completed == 1, "GF did not deliver the completion event.");
            Assert.AreEqual("GestureLab", completedScene);
            yield return AssertCompletionRemains(root, before + 1);
            Assert.AreEqual(1, completed);
        }
        finally
        {
            events.Unsubscribe(NavigationRequestedEventArgs.EventId, requestHandler);
            events.Unsubscribe(NavigationCompletedEventArgs.EventId, completeHandler);
        }

        Assert.IsFalse(events.Check(NavigationRequestedEventArgs.EventId, requestHandler));
        Assert.IsFalse(events.Check(NavigationCompletedEventArgs.EventId, completeHandler));
        yield return LoadLegacyScene("MainMenu");
        yield return WaitForProcedure("ProcedureMainMenu");
        AssertSingleRoot(root);
        Assert.AreEqual(before + 1, root.CompletedNavigations,
            "A legacy load is observed, but is not another accepted GoTo completion.");
        yield return AssertCompletionRemains(root, before + 1);
        Assert.AreEqual(1, completed, "The removed listener must not observe a later scene load.");

        Assert.IsTrue(GameNavigation.GoTo(GameScene.GestureLab));
        yield return WaitForNavigation(root, "GestureLab", "ProcedurePractice", before + 2);
        yield return AssertCompletionRemains(root, before + 2);
        Assert.AreEqual(1, requested, "The removed request listener must stay removed.");
        Assert.AreEqual(1, completed, "The removed completion listener must stay removed.");
    }

    [UnityTest]
    public IEnumerator InvalidDestinationShowsErrorAndSavedCancelButtonRestoresNavigation()
    {
        var root = GameRoot.Instance;
        int before = root.CompletedNavigations;
        var selected = UnityEngine.Object.FindObjectOfType<MainMenuPresenter>()
            .continueText.GetComponentInParent<UnityEngine.UI.Button>();
        Assert.IsNotNull(selected);
        EventSystem.current.SetSelectedGameObject(selected.gameObject);
        Assert.IsFalse(GameNavigation.GoTo("NotAConfiguredScene"));
        Assert.IsTrue(root.HasError);
        Assert.IsFalse(root.IsLoading);
        Assert.IsTrue(GameNavigation.IsInputBlocked);
        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
        Assert.IsNotEmpty(root.loadingOverlay.status.text);
        Assert.IsTrue(root.loadingOverlay.panel.activeInHierarchy);
        Assert.IsTrue(root.loadingOverlay.cancelButton.isActiveAndEnabled);
        Assert.IsTrue(root.loadingOverlay.cancelButton.interactable);
        Assert.AreSame(root.loadingOverlay.cancelButton.gameObject, EventSystem.current.currentSelectedGameObject);
        yield return WaitForProcedure("ProcedureError");
        // A newly enabled Graphic has native absoluteDepth == -1 until Canvas
        // processes it for rendering; UGUI deliberately skips that depth during
        // raycasts. Check this same saved panel after a frame, while the immediate
        // input gate and keyboard focus assertions above remain synchronous.
        yield return null;
        AssertOverlayReceivesPointer(root);

        root.loadingOverlay.cancelButton.onClick.Invoke();
        Assert.IsFalse(root.HasError);
        Assert.IsFalse(GameNavigation.IsInputBlocked);
        Assert.IsFalse(root.loadingOverlay.panel.activeSelf);
        Assert.AreSame(selected.gameObject, EventSystem.current.currentSelectedGameObject,
            "Dismissing an error must restore the outgoing menu's selected button.");
        yield return WaitForProcedure("ProcedureMainMenu");
        Assert.AreEqual(before, root.CompletedNavigations);

        Assert.IsTrue(GameNavigation.GoTo(GameScene.GestureLab),
            "Dismissing an invalid destination must leave navigation usable.");
        yield return WaitForNavigation(root, "GestureLab", "ProcedurePractice", before + 1);
        AssertSingleRoot(root);
    }

    [UnityTest]
    public IEnumerator WorldInputImmediatelyRespectsNavigationErrorAndUnlocksAfterDismissal()
    {
        var root = GameRoot.Instance;
        int before = root.CompletedNavigations;
        Assert.IsTrue(GameNavigation.GoTo(GameScene.Valley));
        yield return WaitForNavigation(root, "Valley", "ProcedureWorld", before + 1);
        var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
        var journey = UnityEngine.Object.FindObjectOfType<JourneyPresenter>();
        Assert.IsNotNull(valley);
        Assert.IsNotNull(valley.explorer);
        Assert.IsFalse(valley.inventoryPanel.activeSelf);
        Assert.IsFalse(valley.pausePanel.activeSelf);
        Assert.IsTrue(journey == null || !journey.IsModal,
            "This regression test requires no other world modal.");
        var explorer = valley.explorer;
        Assert.IsFalse(explorer.inputLocked);
        Assert.IsFalse(explorer.IsInputBlocked);

        Assert.IsFalse(GameNavigation.GoTo("InvalidDestinationWhileExploring"));
        Assert.IsTrue(root.loadingOverlay.panel.activeInHierarchy);
        Assert.IsTrue(explorer.IsInputBlocked,
            "The app gate must block movement and world interactions before another Update runs.");
        yield return null;
        Assert.IsTrue(explorer.inputLocked,
            "The world's next Update must include the app overlay in its modal lock.");
        Assert.IsTrue(explorer.IsInputBlocked);

        root.loadingOverlay.cancelButton.onClick.Invoke();
        yield return null;
        Assert.IsFalse(root.HasError);
        Assert.IsFalse(explorer.inputLocked);
        Assert.IsFalse(explorer.IsInputBlocked,
            "Dismissing the only modal must restore movement, interaction and camera input.");
        Assert.AreEqual(before + 1, root.CompletedNavigations);
        Assert.AreEqual("Valley", SceneManager.GetActiveScene().name);
    }

    private static IEnumerator WaitForNavigation(GameRoot root, string scene, string procedure, int expectedCount)
    {
        yield return WaitFor(() => root != null && !root.IsLoading
            && SceneManager.GetActiveScene().name == scene && root.CurrentProcedure == procedure,
            "Navigation did not reach " + scene + " / " + procedure + ".");
        yield return null; // Complete deferred destruction of a scene-authored duplicate root.
        Assert.AreEqual(scene, root.CurrentScene);
        Assert.AreEqual(expectedCount, root.CompletedNavigations);
        Assert.IsFalse(root.HasError);
        AssertActualProcedure(procedure);
    }

    private static IEnumerator WaitForProcedure(string name)
    {
        yield return WaitFor(() => GameRoot.Instance != null && GameRoot.Instance.CurrentProcedure == name,
            "Application procedure did not enter " + name + ".");
        AssertActualProcedure(name);
    }

    private static void AssertActualProcedure(string name)
    {
        var procedure = GameFrameworkEntry.GetModule<IProcedureManager>().CurrentProcedure;
        Assert.IsNotNull(procedure);
        Assert.AreEqual(name, procedure.GetType().Name,
            "The actual GF state, not just the Inspector label, must match the scene.");
    }

    private static IEnumerator AssertCompletionRemains(GameRoot root, int count)
    {
        // Includes the next-frame GF event dispatch and multiple procedure updates.
        for (int frame = 0; frame < 3; frame++)
        {
            yield return null;
            Assert.AreEqual(count, root.CompletedNavigations);
        }
    }

    private static void AssertOverlayReceivesPointer(GameRoot root)
    {
        Assert.IsNotNull(EventSystem.current, "The saved main menu must contain its input system.");
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * .5f, Screen.height * .5f)
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits, "The visible loading view must receive pointer input.");
        Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(root.transform),
            "Loading must intercept pointer input before controls in the outgoing scene.");
    }

    private static void AssertSingleRoot(GameRoot expected)
    {
        Assert.AreSame(expected, GameRoot.Instance);
        Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<GameRoot>(true).Length,
            "Scene reloads must not leave extra active or inactive application roots.");
    }

    private static IEnumerator LoadLegacyScene(string name)
    {
        var operation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
        Assert.IsNotNull(operation);
        yield return WaitFor(() => operation.isDone, "Legacy scene load timed out: " + name);
        // Let saved presenters initialize and duplicate-root destruction finish.
        yield return null;
    }

    private static IEnumerator WaitFor(Func<bool> condition, string failure)
    {
        float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (!condition())
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, failure);
            yield return null;
        }
    }

    private static void DestroyResidents()
    {
        foreach (var root in UnityEngine.Object.FindObjectsOfType<GameRoot>(true))
            UnityEngine.Object.Destroy(root.gameObject);
        foreach (var session in UnityEngine.Object.FindObjectsOfType<NetSession>(true))
            UnityEngine.Object.Destroy(session.gameObject);
    }
}
