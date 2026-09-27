using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.App;
using Yibi.Battle;
using Yibi.Networking;
using Yibi.Progression;
using Yibi.Rules;
using Yibi.UI;
using Yibi.World;

public class QuestJourneyPlayTests
{
    private IDisposable profileScope;

    [UnitySetUp]
    public IEnumerator IsolateProfileAndNetwork()
    {
        foreach (var session in UnityEngine.Object.FindObjectsOfType<NetSession>(true))
            UnityEngine.Object.Destroy(session.gameObject);
        if (GameRoot.Instance != null && GameRoot.Instance.HasError) GameRoot.Instance.DismissError();
        profileScope = ProfileStore.UseTransientProfile(new PlayerProfile());
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator RestoreProfileAfterLeavingWorld()
    {
        try { yield return Load("Bootstrap"); }
        finally { profileScope?.Dispose(); profileScope = null; }
    }

    [UnityTest]
    public IEnumerator SavedValleyContainsConfiguredJournalAndEditorBoundWorldActions()
    {
        yield return Load("Valley");
        var catalog = Catalog();
        CollectionAssert.IsEmpty(catalog.Validate());
        var journal = UnityEngine.Object.FindObjectOfType<QuestJournalPresenter>();
        Assert.IsNotNull(journal, "The journal must be saved in Valley, not created by this test.");
        Assert.IsNotNull(journal.panel);
        Assert.IsNotNull(journal.content);
        Assert.IsNotNull(journal.rowPrefab);
        Assert.IsNotNull(journal.summary);
        Assert.IsFalse(journal.IsOpen);
        var actions = UnityEngine.Object.FindObjectsOfType<WorldInteraction>();
        Assert.That(actions.Length, Is.GreaterThanOrEqualTo(5));
        foreach (var id in new[] { "practice", "bamboo", "final", "trial", "inventory" })
        {
            var action = actions.Single(x => x.stableId == id);
            Assert.IsNotNull(action.dialogue, id + " must use an editable dialogue asset.");
            Assert.Greater(action.onAccepted.GetPersistentEventCount(), 0,
                id + " must retain its scene action in the Inspector.");
        }
        journal.Open();
        yield return null;
        var rows = journal.content.GetComponentsInChildren<QuestRowView>();
        Assert.AreEqual(catalog.quests.Length, rows.Length);
        CollectionAssert.AreEquivalent(catalog.quests.Select(x => x.stableId), rows.Select(x => x.QuestId));
    }

    [UnityTest]
    public IEnumerator JournalOwnsItsInputLockAndDoesNotReleaseAnotherModal()
    {
        yield return Load("Valley");
        var journal = UnityEngine.Object.FindObjectOfType<QuestJournalPresenter>();
        var valley = journal.valley;
        valley.journey.OpenDialogue(0);
        Assert.IsTrue(valley.journey.IsModal);
        journal.Open();
        Assert.IsTrue(journal.IsOpen);
        Assert.IsFalse(valley.journey.IsModal);
        Assert.IsTrue(valley.explorer.IsInputBlocked, "Opening is synchronous, before the next Update.");
        yield return null;
        Assert.IsTrue(valley.explorer.IsInputBlocked, "World Update must continue respecting the journal.");
        valley.pausePanel.SetActive(true);
        journal.Close();
        Assert.IsTrue(valley.explorer.IsInputBlocked, "Closing this panel cannot unlock an existing pause panel.");
        valley.Resume();
        yield return null;
        Assert.IsFalse(valley.explorer.IsInputBlocked);
        journal.Toggle();
        yield return null;
        Assert.IsTrue(valley.explorer.IsInputBlocked);
        journal.Toggle();
        yield return null;
        Assert.IsFalse(valley.explorer.IsInputBlocked);
    }

    [UnityTest]
    public IEnumerator DialogueRequirementsAndProseComeFromTheAssignedAsset()
    {
        yield return Load("Valley");
        var action = UnityEngine.Object.FindObjectsOfType<WorldInteraction>().Single(x => x.stableId == "final");
        var original = action.dialogue;
        var clone = UnityEngine.Object.Instantiate(original);
        try
        {
            action.dialogue = clone;
            clone.speaker = "Asset-controlled test speaker";
            clone.body = "Asset-controlled test prose";
            clone.requiredQuestIds = Array.Empty<string>();
            Assert.IsTrue(action.CanExecute, "Removing asset prerequisites opens this saved interaction.");
            var journey = UnityEngine.Object.FindObjectOfType<JourneyPresenter>();
            journey.OpenDialogue(2);
            Assert.IsTrue(journey.accept.interactable);
            Assert.AreEqual(clone.speaker, journey.dialogueTitle.text);
            Assert.AreEqual(clone.body, journey.dialogueBody.text);
            journey.CloseDialogue();
            clone.requiredQuestIds = new[] { "test.unfinished.requirement" };
            clone.blockedBody = "Complete the configured prerequisite first.";
            Assert.IsFalse(action.CanExecute);
            Assert.IsFalse(action.TryExecute(), "A disabled UI must also have a guarded gameplay command.");
            journey.OpenDialogue(2);
            Assert.IsFalse(journey.accept.interactable);
            Assert.AreEqual(clone.blockedBody, journey.dialogueBody.text);
        }
        finally
        {
            action.dialogue = original;
            UnityEngine.Object.Destroy(clone);
        }
    }

    [UnityTest]
    public IEnumerator ProgramSampleCannotAdvanceQuestsButTheInputPipelineCan()
    {
        yield return Load("GestureLab");
        var progress = UnityEngine.Object.FindObjectOfType<PracticeProgress>();
        Assert.IsNotNull(progress);
        var quest = Definition("guiyun.practice");
        int before = ProfileStore.Current.tickets;
        progress.board.ShowSample(FixedGestureSamples.Create(progress.board.template.ToRules())[0].Points);
        Assert.IsFalse(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(before, ProfileStore.Current.tickets);
        Assert.IsFalse(ProfileStore.Current.achievements.Contains("practice-pass"));
        DrawThroughInputPipeline(progress.board);
        Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(before + quest.reward.tickets, ProfileStore.Current.tickets);
        int rewarded = ProfileStore.Current.tickets;
        DrawThroughInputPipeline(progress.board);
        Assert.AreEqual(rewarded, ProfileStore.Current.tickets);
        // The path is scripted through the real sampler; this is not a human mouse usability test.
    }

    [UnityTest]
    public IEnumerator PracticeAndFinalExamRemainRetryableWhenPersistenceFails()
    {
        yield return Load("GestureLab");
        var session = UnityEngine.Object.FindObjectOfType<PracticeSession>();
        session.ToggleExam();
        using (ProfileStore.UseSaveOverride(_ => { throw new System.IO.IOException("Simulated practice save failure"); }))
            DrawThroughInputPipeline(session.lab.board);
        Assert.IsFalse(session.AwaitingNext);
        Assert.AreEqual(0, session.ExamStep);
        Assert.AreEqual(0, ProfileStore.Current.practice.Count);
        Assert.AreEqual(2, ProfileStore.Current.tickets);
        StringAssert.Contains("保存失败", session.examStatus.text);

        for (int index = 0; index < session.lab.routes.Length; index++)
        {
            DrawThroughInputPipeline(session.lab.board);
            Assert.IsTrue(session.AwaitingNext);
            if (index + 1 < session.lab.routes.Length) session.Next();
        }
        var quest = Definition("guiyun.exam");
        int before = ProfileStore.Current.tickets;
        using (ProfileStore.UseSaveOverride(_ => { throw new System.IO.IOException("Simulated exam save failure"); }))
            session.Next();
        Assert.IsTrue(session.ExamActive);
        Assert.IsTrue(session.AwaitingNext, "The finish button must stay usable for an explicit retry.");
        Assert.IsFalse(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(before, ProfileStore.Current.tickets);
        StringAssert.Contains("保存失败", session.examStatus.text);

        session.Next();
        Assert.IsFalse(session.ExamActive);
        Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(before + quest.reward.tickets, ProfileStore.Current.tickets);
        session.Next();
        Assert.AreEqual(before + quest.reward.tickets, ProfileStore.Current.tickets);
    }

    [UnityTest]
    public IEnumerator SavedTrialCheckpointsReportCompletionAndPayOnlyOnce()
    {
        yield return Load("Valley");
        var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
        var trial = UnityEngine.Object.FindObjectsOfType<WorldInteraction>().Single(x => x.stableId == "trial");
        var quest = Definition("guiyun.trial");
        int before = ProfileStore.Current.tickets;
        for (int repeat = 0; repeat < 2; repeat++)
        {
            Assert.IsTrue(trial.TryExecute());
            for (int i = 0; i < valley.checkpoints.Length; i++)
            {
                var checkpoint = valley.checkpoints[i];
                Assert.IsTrue(checkpoint.gameObject.activeSelf, "Only the next saved checkpoint is expected to be lit.");
                var controller = valley.explorer.GetComponent<CharacterController>();
                controller.enabled = false;
                valley.explorer.transform.position = checkpoint.position;
                controller.enabled = true;
                yield return Until(() => !checkpoint.gameObject.activeSelf, 3,
                    "The real Valley distance check did not accept checkpoint " + i + ".");
            }
            Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
            Assert.AreEqual(before + quest.reward.tickets, ProfileStore.Current.tickets);
        }
        // This verifies saved checkpoints and the gameplay report path, not traversal difficulty.
    }

    [UnityTest]
    public IEnumerator LegacySaveSurvivesSceneReloadWithoutRewardsFromViewRefresh()
    {
        var legacy = new PlayerProfile { schemaVersion = 1, tickets = 23, fragments = 4 };
        legacy.achievements.AddRange(new[] { "practice-pass", "first-win-0", "valley-trial", "first-win-1", "chapter-one" });
        using (ProfileStore.UseTransientProfile(legacy))
        {
            for (int repeat = 0; repeat < 2; repeat++)
            {
                yield return Load("Valley");
                var journal = UnityEngine.Object.FindObjectOfType<QuestJournalPresenter>();
                for (int i = 0; i < 3; i++)
                {
                    journal.valley.journey.Refresh();
                    journal.Open();
                    journal.Refresh();
                    journal.Close();
                }
                Assert.IsTrue(ProfileStore.Progress.IsClaimed("guiyun.chapter"));
                Assert.AreEqual(23, legacy.tickets);
                Assert.AreEqual(4, legacy.fragments);
                yield return Load("Bootstrap");
            }
        }
    }

    [UnityTest]
    public IEnumerator RealOfflineVictoryGrantsAtTheResultBoundaryAndRefreshCannotRepay()
    {
        yield return Load("Arena_Stone");
        var battle = UnityEngine.Object.FindObjectOfType<BattlePresenter>();
        Assert.IsFalse(battle.IsOnline);
        var quest = Definition("guiyun.bamboo");
        int before = ProfileStore.Current.tickets;
        float deadline = Time.realtimeSinceStartup + 90;
        // This sequence uses the default, owned loadout and default NPC strategy.
        // No fighter HP, energy, winner, inventory or AI state is changed by the test.
        foreach (string id in new[] { "lieshi", "rest", "dianxue", "lieshi", "rest", "dianxue", "lieshi", "basic" })
        {
            while (battle.State.winner == -2 && !(battle.State.activeSeat == 0 && battle.basicButton.interactable))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Battle did not return control to the player.");
                yield return null;
            }
            if (battle.State.winner != -2) break;
            Assert.IsFalse(ProfileStore.Progress.IsClaimed(quest.stableId), "No reward before a real victory.");
            int revision = battle.State.revision;
            if (id == "rest") battle.Rest();
            else if (id == "basic")
            {
                // The final basic attack is the actual finishing blow for this unchanged battle.
                using (ProfileStore.UseSaveOverride(_ => { throw new System.IO.IOException("Simulated victory save failure"); }))
                    battle.Basic();
            }
            else
            {
                int slot = Array.IndexOf(battle.State.fighters[0].loadout, id);
                Assert.GreaterOrEqual(slot, 0);
                Assert.IsNull(BattleReducer.CanUse(battle.State, 0, id));
                battle.SelectSkill(slot);
                Assert.IsTrue(battle.drawingPanel.activeSelf);
                DrawThroughInputPipeline(battle.board);
                Assert.IsTrue(battle.confirmButton.interactable);
                battle.Confirm();
            }
            Assert.Greater(battle.State.revision, revision, "The UI command must reach the actual reducer.");
            yield return null;
        }
        while (!battle.resultPanel.activeSelf)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "The battle never reached its result presentation.");
            yield return null;
        }
        Assert.AreEqual(0, battle.State.winner);
        Assert.IsTrue(battle.HasPendingVictory);
        Assert.IsFalse(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(before, ProfileStore.Current.tickets);
        StringAssert.Contains("待保存", battle.resultLabel.text);
        for (int i = 0; i < 10; i++) battle.Refresh();
        Assert.IsFalse(ProfileStore.Progress.IsClaimed(quest.stableId), "Refreshing the result cannot retry a failed write.");
        string completedMatch = battle.State.matchId;
        using (ProfileStore.UseSaveOverride(_ => { throw new System.IO.IOException("Simulated victory retry failure"); }))
        {
            battle.Restart();
            battle.ReturnToPractice();
            Assert.IsFalse(battle.RetryPendingVictory());
        }
        Assert.AreEqual(completedMatch, battle.State.matchId, "Restart cannot discard an unsaved victory.");
        Assert.AreEqual("Arena_Stone", SceneManager.GetActiveScene().name, "Leaving cannot discard an unsaved victory.");
        Assert.IsTrue(battle.resultPanel.activeSelf);
        Assert.IsTrue(battle.RetryPendingVictory());
        Assert.IsFalse(battle.HasPendingVictory);
        Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(before + quest.reward.tickets, ProfileStore.Current.tickets);
        string paid = JsonUtility.ToJson(ProfileStore.Current);
        for (int i = 0; i < 10; i++) battle.Refresh();
        yield return null;
        Assert.AreEqual(paid, JsonUtility.ToJson(ProfileStore.Current));
    }

    [UnityTest]
    public IEnumerator TrialSaveFailureRetainsTheCompletedRunForOneExplicitRetry()
    {
        yield return Load("Valley");
        var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
        var trial = valley.FindInteraction("trial");
        var quest = Definition("guiyun.trial");
        string before = JsonUtility.ToJson(ProfileStore.Current);
        int tickets = ProfileStore.Current.tickets;
        using (ProfileStore.UseSaveOverride(_ => { throw new System.IO.IOException("Simulated trial save failure"); }))
        {
            Assert.IsTrue(trial.TryExecute());
            foreach (var checkpoint in valley.checkpoints)
            {
                var controller = valley.explorer.GetComponent<CharacterController>();
                controller.enabled = false; valley.explorer.transform.position = checkpoint.position; controller.enabled = true;
                yield return Until(() => !checkpoint.gameObject.activeSelf, 3, "Checkpoint was not reached.");
            }
            yield return null;
            Assert.IsTrue(valley.HasPendingTrial);
            Assert.IsFalse(valley.TrialActive);
            Assert.AreEqual(before, JsonUtility.ToJson(ProfileStore.Current));
            StringAssert.Contains("保存失败", valley.notice.text);
            StringAssert.Contains("等待保存", valley.trialText.text);
            valley.GoToPractice();
            Assert.AreEqual("Valley", SceneManager.GetActiveScene().name);
            Assert.IsFalse(GameNavigation.IsInputBlocked, "The failed save must stop before starting navigation.");
        }
        valley.journey.OpenDialogue(3);
        valley.journey.accept.onClick.Invoke();
        Assert.IsFalse(valley.HasPendingTrial);
        Assert.IsFalse(valley.TrialActive, "Retrying the completed run must not demand another traversal.");
        Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
        Assert.AreEqual(tickets + quest.reward.tickets, ProfileStore.Current.tickets);
        string paid = JsonUtility.ToJson(ProfileStore.Current);
        Assert.IsTrue(valley.RetryPendingTrial());
        valley.journey.Refresh();
        Assert.AreEqual(paid, JsonUtility.ToJson(ProfileStore.Current));
    }

    private static void DrawThroughInputPipeline(GestureBoard board)
    {
        var nodes = board.template.nodes;
        double start = Time.unscaledTimeAsDouble;
        board.Begin(new Point2(nodes[0].x, nodes[0].y), start);
        for (int i = 1; i < nodes.Length - 1; i++)
            board.Move(new Point2(nodes[i].x, nodes[i].y), start + i * .1);
        var end = nodes[nodes.Length - 1];
        board.Finish(new Point2(end.x, end.y), start + 1);
        Assert.IsNotNull(board.LastResult);
        Assert.IsTrue(board.LastResult.Valid, board.LastResult.ErrorCode.ToString());
        Assert.GreaterOrEqual(board.LastResult.Score, 90);
        Assert.IsFalse(board.IsProgramSample);
    }

    private static QuestCatalogSO Catalog()
    {
        var catalog = Resources.Load<QuestCatalogSO>("Progression/QuestCatalog");
        Assert.IsNotNull(catalog, "The saved Stage B catalog must be installed before PlayMode verification.");
        return catalog;
    }

    private static QuestDefinitionSO Definition(string id) { return Catalog().quests.Single(x => x.stableId == id); }

    private static IEnumerator Load(string scene)
    {
        var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        Assert.IsNotNull(operation);
        yield return Until(() => operation.isDone, 30, "Loading timed out: " + scene);
        yield return null;
    }

    private static IEnumerator Until(Func<bool> predicate, float seconds, string failure)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate())
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, failure);
            yield return null;
        }
    }
}
