using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.UI;
using Yibi.World;
using Yibi.Battle;
using Yibi.Rules;

public class PolishPlayTests
{
    IDisposable profile;
    [SetUp] public void Setup(){profile=ProfileStore.UseTransientProfile(new PlayerProfile());}
    [UnityTearDown] public IEnumerator Teardown(){yield return SceneManager.LoadSceneAsync("Bootstrap");profile.Dispose();}
    static void Draw(GestureBoard board){var nodes=board.template.ToRules().Nodes;board.Begin(nodes[0],0);for(int i=1;i<nodes.Length-1;i++)board.Move(nodes[i],i*.1);board.Finish(nodes[nodes.Length-1],2);}
    [UnityTest] public IEnumerator SixRouteExamRejectsProgramSamplesAndRewardsOnlyOnce()
    {
        yield return SceneManager.LoadSceneAsync("GestureLab");yield return null;var session=UnityEngine.Object.FindObjectOfType<PracticeSession>();var board=session.lab.board;Assert.AreEqual(6,session.lab.routes.Length);Assert.AreEqual(1,UnityEngine.Object.FindObjectsOfType<PracticeSession>().Length);Assert.AreEqual(1,board.GetComponents<GestureBoardFeedback>().Length);
        foreach(var parent in session.lab.GetComponentsInChildren<Transform>(true))Assert.IsFalse(parent.Cast<Transform>().GroupBy(t=>t.name).Any(g=>g.Count()>1),"Overlapping duplicate UI children: "+parent.name);
        Assert.AreNotSame(session.history,session.lab.sampleText);Assert.AreEqual("此经脉尚无手绘记录",session.history.text);
        session.ToggleExam();session.lab.NextSample();Assert.IsFalse(session.AwaitingNext);Assert.AreEqual(0,ProfileStore.Current.practice.Count);
        var coach=board.GetComponent<GestureBoardFeedback>();coach.Demonstrate();yield return null;Assert.IsFalse(board.inputEnabled);Assert.IsNull(board.LastResult);Assert.AreEqual(0,ProfileStore.Current.practice.Count);
        session.lab.SelectRoute(1);Assert.IsFalse(coach.IsPlayingBack);Assert.IsTrue(board.inputEnabled);session.lab.SelectRoute(0);
        for(int round=0;round<2;round++){if(round>0)session.ToggleExam();for(int i=0;i<6;i++){Draw(board);Assert.IsTrue(session.AwaitingNext);Assert.AreEqual(100,board.LastResult.Score);session.Next();}}
        Assert.AreEqual(6,ProfileStore.Current.practice.Count);Assert.AreEqual(6,ProfileStore.Current.tickets,"Initial 2 + chapter practice 1 + exam 3 only once");Assert.IsTrue(ProfileStore.Current.achievements.Contains("meridian-exam"));
    }
    [UnityTest] public IEnumerator ReplayPreservesOriginalInputAndNeverAddsAnAttempt()
    {
        yield return SceneManager.LoadSceneAsync("GestureLab");yield return null;var lab=UnityEngine.Object.FindObjectOfType<GestureLabPresenter>();var board=lab.board;Draw(board);var result=board.LastResult;var points=board.Sampler.Points.Count;int callbacks=0;board.Scored+=s=>callbacks++;
        var coach=board.GetComponent<GestureBoardFeedback>();coach.Replay();yield return new WaitForSecondsRealtime(.15f);Assert.IsTrue(coach.IsPlayingBack);Assert.IsFalse(board.inputEnabled);coach.StopPlayback();
        Assert.AreSame(result,board.LastResult);Assert.AreEqual(points,board.Sampler.Points.Count);Assert.AreEqual(1,ProfileStore.Current.practice[0].attempts);Assert.AreEqual(0,callbacks);Assert.IsTrue(board.inputEnabled);Assert.IsTrue(board.playerLine.enabled);
    }
    [UnityTest] public IEnumerator SavedSkyResidentsAndDialogueAreConnected()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;Assert.AreEqual("Yibi/Guiyun Sky",RenderSettings.skybox.shader.name);Assert.AreEqual(CameraClearFlags.Skybox,Camera.main.clearFlags);
        yield return SceneManager.LoadSceneAsync("Valley");yield return null;var people=UnityEngine.Object.FindObjectsOfType<ValleyResident>();Assert.AreEqual(4,people.Length);
        var journey=UnityEngine.Object.FindObjectOfType<JourneyPresenter>();journey.OpenResident(people[0]);yield return null;Assert.IsTrue(journey.valley.explorer.inputLocked);Assert.AreEqual(people[0].dialogue,journey.dialogueBody.text);journey.AcceptDialogue();yield return null;Assert.IsFalse(journey.valley.explorer.inputLocked);
        Assert.Less(journey.valley.explorer.cameraOffset.y,12,"Camera override must survive saving/reloading");
        var gate=GameObject.Find("洛阳牌坊");Assert.IsNotNull(gate.GetComponent<MeshCollider>());Assert.Less(gate.transform.position.z,-35,"Entrance gate must be behind the initial camera");
    }
    [UnityTest] public IEnumerator CameraPullsInBeforeBlockingGeometry()
    {
        yield return SceneManager.LoadSceneAsync("Valley");yield return null;var explorer=UnityEngine.Object.FindObjectOfType<ValleyExplorer>();explorer.inputLocked=true;
        var target=explorer.transform.position+Vector3.up*1.6f;var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=target+explorer.cameraOffset.normalized*5;blocker.transform.localScale=Vector3.one*2;Physics.SyncTransforms();yield return null;yield return null;
        Assert.Less(Vector3.Distance(explorer.followCamera.transform.position,explorer.transform.position+Vector3.up*1.6f),5);UnityEngine.Object.Destroy(blocker);
    }
}
