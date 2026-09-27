using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.Battle;
using Yibi.Networking;
using Yibi.Rules;

public class StageCBattlePlayTests
{
    private System.IDisposable profile;
    private int oldStrategy;

    [UnitySetUp]
    public IEnumerator Isolate()
    {
        foreach (var session in Object.FindObjectsOfType<NetSession>(true)) Object.Destroy(session.gameObject);
        oldStrategy = ProfileStore.NpcStrategy;
        ProfileStore.NpcStrategy = 0;
        profile = ProfileStore.UseTransientProfile(new PlayerProfile());
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        yield return SceneManager.LoadSceneAsync("Bootstrap");
        profile.Dispose();
        ProfileStore.NpcStrategy = oldStrategy;
    }

    [UnityTest]
    public IEnumerator BothSavedArenasContainBoundProfilesAndEditableHud()
    {
        foreach (string scene in new[] { "Arena_Stone", "Arena_Bamboo" })
        {
            yield return SceneManager.LoadSceneAsync(scene); yield return null;
            var battle = Object.FindObjectOfType<BattlePresenter>();
            Assert.IsNotNull(battle.presentation, scene);
            Assert.AreEqual(2, battle.npcTactics.Length);
            Assert.IsNotNull(battle.npcTactics[0]); Assert.IsNotNull(battle.npcTactics[1]);
            Assert.IsNotNull(battle.hud); Assert.AreSame(battle, battle.hud.battle);
            Assert.IsNotNull(battle.hud.intentLabel); Assert.IsNotNull(battle.hud.selectionLabel);
            Assert.IsNotNull(battle.hud.gradeLabel); Assert.IsNotNull(battle.hud.breakdownLabel);
            Assert.IsNotNull(battle.hud.recoveryLabel); Assert.IsNotNull(battle.hud.settlementLabel);
            Assert.IsNotNull(battle.hud.leftEffects); Assert.IsNotNull(battle.hud.rightEffects);
            Assert.AreEqual(2, battle.feedback.attackEffects.Length);
            Assert.IsNotNull(battle.feedback.attackEffects[0]); Assert.IsNotNull(battle.feedback.attackEffects[1]);
            battle.SelectSkill(0);
            Assert.IsTrue(battle.drawingPanel.activeSelf);
            Assert.IsTrue(battle.hud.selectionLabel.gameObject.activeInHierarchy);
            StringAssert.Contains(battle.TechniqueTitle(battle.SelectedSkill), battle.hud.selectionLabel.text);
        }
    }

    [UnityTest]
    public IEnumerator ThreeInputErrorsCanRedrawWithoutConsumingTurnOrKeepingStaleConfirmation()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone"); yield return null;
        var battle = Object.FindObjectOfType<BattlePresenter>(); battle.SelectSkill(0);
        var board = battle.board; var nodes = board.template.nodes;
        var start = new Point2(nodes[0].x, nodes[0].y); var end = new Point2(nodes[nodes.Length - 1].x, nodes[nodes.Length - 1].y);
        foreach (var error in new[] { GestureError.TooShort, GestureError.WrongStart, GestureError.OutOfBounds })
        {
            if (error == GestureError.TooShort) { board.Begin(start, 0); board.Finish(start, .1); }
            else if (error == GestureError.WrongStart) { board.Begin(new Point2(.01, .01), 0); board.Finish(end, .1); }
            else { board.Begin(start, 0); board.Move(new Point2(-.1, .5), .1); board.Finish(end, .2); }
            Assert.AreEqual(error, board.LastResult.ErrorCode);
            Assert.AreEqual(0, battle.State.revision);
            StringAssert.Contains("重新起笔", battle.hud.recoveryLabel.text);
            Canvas.ForceUpdateCanvases();
            Assert.LessOrEqual(battle.hud.recoveryLabel.preferredHeight,battle.hud.recoveryLabel.rectTransform.rect.height,"Recovery cost must be visibly readable, not just present in a clipped string.");
            StringAssert.Contains("吐纳掌",battle.confirmButton.GetComponentInChildren<UnityEngine.UI.Text>().text);
            Assert.IsTrue(battle.confirmButton.interactable, "An explicitly confirmed invalid stroke remains the documented basic fallback.");
            board.Begin(start, 1);
            Assert.IsFalse(battle.confirmButton.interactable, "Starting a new stroke invalidates the prior result immediately.");
            battle.Confirm(); Assert.AreEqual(0, battle.State.revision);
            for (int i = 1; i < nodes.Length - 1; i++) board.Move(new Point2(nodes[i].x, nodes[i].y), 1 + i * .1);
            board.Finish(end, 2);
            Assert.IsTrue(board.LastResult.Valid); Assert.AreEqual(100, board.LastResult.Score);
            Assert.IsTrue(battle.confirmButton.interactable);
            board.Clear(); Assert.IsFalse(battle.confirmButton.interactable);
            Assert.AreEqual(0, battle.State.revision);
        }
    }

    [UnityTest]
    public IEnumerator ProgramSampleCannotSubmitAndDisablingBoardInvalidatesResult()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone"); yield return null;
        var battle = Object.FindObjectOfType<BattlePresenter>(); battle.SelectSkill(0);
        battle.board.ShowSample(BattleContent.Perfect(battle.SelectedSkill));
        Assert.IsTrue(battle.board.IsProgramSample);
        Assert.IsFalse(battle.confirmButton.interactable);
        battle.Confirm(); Assert.AreEqual(0, battle.State.revision);
        StringAssert.Contains("程序样本", battle.hud.gradeLabel.text);
        battle.board.gameObject.SetActive(false);
        Assert.IsNull(battle.board.LastResult); Assert.AreEqual(0, battle.board.Sampler.Points.Count);
        battle.board.gameObject.SetActive(true);
        Assert.IsFalse(battle.confirmButton.interactable);
        DrawPerfect(battle);
        Assert.IsFalse(battle.board.IsProgramSample); Assert.IsTrue(battle.confirmButton.interactable);
    }

    [UnityTest]
    public IEnumerator ConfirmAndDirectRepeatResolveCannotConsumeTheNextActorsTurn()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone"); yield return null;
        var battle = Object.FindObjectOfType<BattlePresenter>(); battle.SelectSkill(0); DrawPerfect(battle);
        battle.Confirm();
        Assert.AreEqual(1, battle.State.revision); Assert.AreEqual(1, battle.State.activeSeat);
        var accepted = battle.State;
        battle.Confirm(); battle.Basic(); battle.Resolve("basic");
        Assert.AreSame(accepted, battle.State);
        StringAssert.Contains("100分", battle.hud.settlementLabel.text);
        StringAssert.Contains("护盾 5→0", battle.hud.settlementLabel.text);
        Assert.IsFalse(battle.drawingPanel.activeSelf);
        float npcWait = (battle.presentation == null ? 2.1f : battle.presentation.actionDuration + battle.presentation.npcThinkDelay) + 1f;
        float npcDeadline = Time.realtimeSinceStartup + Mathf.Clamp(npcWait, 1f, 10f);
        while (battle.State.turnId < 3 && Time.realtimeSinceStartup < npcDeadline) yield return null;
        Assert.AreEqual(3, battle.State.turnId, "NPC must remain able to act within the configured presentation and thinking time budget.");
    }

    [UnityTest]
    public IEnumerator ConfirmedInvalidStrokeUsesBasicFallbackWithoutTechniqueCost()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone"); yield return null;
        var battle = Object.FindObjectOfType<BattlePresenter>(); battle.SelectSkill(0);
        var node = battle.board.template.nodes[0];
        battle.board.Begin(new Point2(node.x, node.y), 0); battle.board.Finish(new Point2(node.x, node.y), .1);
        battle.Confirm();
        Assert.AreEqual(1, battle.State.revision);
        Assert.AreEqual(3, battle.State.fighters[0].energy);
        Assert.AreEqual(0, battle.State.fighters[0].eligible[0]);
        Assert.AreEqual(99, battle.State.fighters[1].hp);
        StringAssert.Contains("运功失误，改为吐纳掌", battle.hud.settlementLabel.text);
    }

    static void DrawPerfect(BattlePresenter battle)
    {
        var nodes = battle.board.template.nodes;
        battle.board.Begin(new Point2(nodes[0].x, nodes[0].y), 0);
        for (int i = 1; i < nodes.Length - 1; i++) battle.board.Move(new Point2(nodes[i].x, nodes[i].y), i * .1);
        var last = nodes[nodes.Length - 1]; battle.board.Finish(new Point2(last.x, last.y), 1);
    }
}
