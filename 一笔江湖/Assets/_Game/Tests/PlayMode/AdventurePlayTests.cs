using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.World;
using Yibi.Battle;
using Yibi.Presentation;
using Yibi.Rules;
using Yibi.UI;

public class AdventurePlayTests
{
    private IDisposable profile;
    [SetUp] public void Setup(){profile=ProfileStore.UseTransientProfile(new PlayerProfile());}
    [UnityTearDown] public IEnumerator Teardown(){yield return SceneManager.LoadSceneAsync("Bootstrap");profile.Dispose();}
    [UnityTest] public IEnumerator SavedValleyDialogueGateTreasureAndExplicitEquipWork()
    {
        yield return SceneManager.LoadSceneAsync("Valley");yield return null;
        var j=UnityEngine.Object.FindObjectOfType<JourneyPresenter>();Assert.IsNotNull(j);Assert.IsNotNull(j.valley.explorer.GetComponentInChildren<CharacterMotion>());
        j.OpenDialogue(2);yield return null;Assert.IsTrue(j.valley.explorer.inputLocked);Assert.IsFalse(j.accept.interactable);
        ProfileStore.Reward("first-win-0",0);ProfileStore.Reward("valley-trial",0);j.OpenDialogue(2);Assert.IsTrue(j.accept.interactable);j.CloseDialogue();yield return null;Assert.IsFalse(j.valley.explorer.inputLocked);
        int before=ProfileStore.Current.tickets;Assert.IsTrue(j.caches[0].Collect());Assert.IsFalse(j.caches[0].Collect());Assert.AreEqual(before+1,ProfileStore.Current.tickets);
        j.SelectTechnique(1);j.EquipTechnique(0);CollectionAssert.AreEqual(new[]{"lieshi","dianxue","huifeng"},ProfileStore.Current.loadout);ProfileStore.Validate(ProfileStore.Current);
        j.valley.ToggleInventory();yield return null;Assert.IsTrue(j.valley.explorer.inputLocked);Assert.Greater(j.valley.inventoryPanel.GetComponent<Canvas>().sortingOrder,0);
    }
    [UnityTest] public IEnumerator ProgramSampleCannotClaimPracticeButInputPipelineCan()
    {
        yield return SceneManager.LoadSceneAsync("GestureLab");yield return null;
        var p=UnityEngine.Object.FindObjectOfType<PracticeProgress>();Assert.IsNotNull(p);var board=p.board;
        board.ShowSample(FixedGestureSamples.Create(board.template.ToRules())[0].Points);Assert.IsFalse(ProfileStore.Current.achievements.Contains("practice-pass"));
        var nodes=board.template.nodes;board.Begin(new Point2(nodes[0].x,nodes[0].y),0);for(int i=1;i<nodes.Length-1;i++)board.Move(new Point2(nodes[i].x,nodes[i].y),i*.1);var end=nodes[nodes.Length-1];board.Finish(new Point2(end.x,end.y),1);
        Assert.IsTrue(ProfileStore.Current.achievements.Contains("practice-pass"));Assert.AreEqual(3,ProfileStore.Current.tickets);
        // This is scripted input-pipeline verification, not evidence of a human drawing test.
    }
    [UnityTest] public IEnumerator SavedArenaAnimatesActionAndRestartRestoresActorPosition()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone");yield return null;var p=UnityEngine.Object.FindObjectOfType<BattlePresenter>();var motion=p.leftActor.GetComponentInChildren<CharacterMotion>();Assert.IsNotNull(motion);var origin=p.leftActor.localPosition;
        p.Basic();yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual("Attack",motion.CurrentAction);Assert.Greater(Vector3.Distance(origin,p.leftActor.localPosition),.05f);
        p.Restart();Assert.AreEqual(origin,p.leftActor.localPosition);Assert.AreEqual("Locomotion",motion.CurrentAction);Assert.AreEqual(1,p.State.turnId);
    }
    [UnityTest] public IEnumerator TitleAndValleyReturnKeepProgress()
    {
        ProfileStore.Reward("practice-pass",1);yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        var menu=UnityEngine.Object.FindObjectOfType<MainMenuPresenter>();Assert.IsNotNull(menu);menu.ToggleHelp();Assert.IsTrue(menu.help.activeSelf);menu.Continue();
        float deadline=Time.realtimeSinceStartup+30;while(SceneManager.GetActiveScene().name!="Valley"){Assert.Less(Time.realtimeSinceStartup,deadline,"Continue did not reach Valley within 30 seconds.");yield return null;}yield return null;
        Assert.AreEqual("Valley",SceneManager.GetActiveScene().name);UnityEngine.Object.FindObjectOfType<ValleyPresenter>().ReturnToTitle();
        deadline=Time.realtimeSinceStartup+30;while(SceneManager.GetActiveScene().name!="MainMenu"){Assert.Less(Time.realtimeSinceStartup,deadline,"ReturnToTitle did not reach MainMenu within 30 seconds.");yield return null;}yield return null;
        Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name);Assert.AreEqual(3,ProfileStore.Current.tickets);Assert.IsTrue(ProfileStore.Current.achievements.Contains("practice-pass"));
    }
}
