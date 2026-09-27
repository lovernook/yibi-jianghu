using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.Networking;
using Yibi.Rules;
using Yibi.Battle;
using Yibi.World;
public class NetworkClientPlayTests
{
    IDisposable profile;
    [SetUp] public void Setup(){profile=ProfileStore.UseTransientProfile(new PlayerProfile());}
    [UnityTearDown] public IEnumerator Cleanup(){if(NetSession.Instance!=null)UnityEngine.Object.Destroy(NetSession.Instance.gameObject);yield return null;yield return SceneManager.LoadSceneAsync("Bootstrap");profile.Dispose();}
    static void Set(NetSession n,string name,object value){typeof(NetSession).GetProperty(name).SetValue(n,value);}
    static void Apply(NetSession n,string type,BattleState state){typeof(NetSession).GetMethod("ApplySnapshot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(n,new object[]{new WireMessage{type=type,payload=new WirePayload{room="123456",state=state,remaining=20}}});}
    [UnityTest] public IEnumerator StaleSnapshotsAndDuplicateRepliesCannotRollBackClient()
    {
        var n=NetSession.Ensure();Set(n,"Seat",0);Set(n,"Room","123456");Set(n,"Connected",true);var first=BattleReducer.Create("first",0);first.revision=3;
        Assert.IsFalse(n.CanAct);Apply(n,"StateSnapshot",first);Assert.IsTrue(n.CanAct);var duplicate=first.Copy();Apply(n,"ActionResolved",duplicate);Assert.AreSame(first,n.State);
        var stale=first.Copy();stale.revision=2;Apply(n,"StateSnapshot",stale);Assert.AreSame(first,n.State);
        var other=BattleReducer.Create("other",0);Apply(n,"ActionResolved",other);Assert.AreSame(first,n.State);Apply(n,"MatchStarted",other);Assert.AreSame(other,n.State);
        Set(n,"Recovering",true);Assert.IsFalse(n.CanAct);Set(n,"Connected",false);yield return null;
    }
    [UnityTest] public IEnumerator SavedNetworkScreensAndMemoryPoliciesHaveReferences()
    {
        yield return SceneManager.LoadSceneAsync("Lobby");yield return null;var lobby=UnityEngine.Object.FindObjectOfType<LobbyPresenter>();Assert.IsNotNull(lobby.roomSummary);Assert.IsNotNull(lobby.readyButton);Assert.Greater(lobby.readyButton.onClick.GetPersistentEventCount(),0);
        foreach(var scene in new[]{"Arena_Stone","Arena_Bamboo"}){
            yield return SceneManager.LoadSceneAsync(scene);yield return null;
            var battle=UnityEngine.Object.FindObjectOfType<BattlePresenter>();var p=battle.GetComponent<NetworkBattlePanel>();
            Assert.IsNotNull(p);Assert.IsNotNull(p.status);Assert.Greater(p.rematch.onClick.GetPersistentEventCount(),0);
            Assert.IsFalse(p.panel.activeSelf,"Offline should not show network controls");
            Assert.AreEqual(1,UnityEngine.Object.FindObjectsOfType<Yibi.Core.SceneMemoryPolicy>().Length);
            Canvas.ForceUpdateCanvases();
            var clockCorners=new Vector3[4];var networkCorners=new Vector3[4];
            battle.turnLabel.rectTransform.GetWorldCorners(clockCorners);
            p.panel.GetComponent<RectTransform>().GetWorldCorners(networkCorners);
            Assert.Greater(clockCorners[0].y,networkCorners[1].y,"Network bar must not cover turn label, including when parents differ");
            Assert.IsNull(battle.transform.Find("恢复连接"),"Only one recovery entry");
        }
    }
    [UnityTest] public IEnumerator AbortedServiceDisplaysNoLocalWinnerAndRestartReturnsToLobby()
    {
        var n=NetSession.Ensure();Set(n,"Seat",0);Set(n,"Room","123456");Apply(n,"StateSnapshot",BattleReducer.Create("abort",0));
        yield return SceneManager.LoadSceneAsync("Arena_Stone");yield return null;var b=UnityEngine.Object.FindObjectOfType<BattlePresenter>();Assert.IsTrue(b.IsOnline);
        typeof(NetSession).GetMethod("Abort",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(n,new object[]{"服务已中止"});yield return null;
        Assert.AreEqual(-2,b.State.winner);Assert.IsTrue(b.resultPanel.activeSelf);Assert.IsTrue(b.resultLabel.text.Contains("不记胜负"));Assert.IsFalse(b.basicButton.interactable);b.Restart();yield return null;yield return null;Assert.AreEqual("Lobby",SceneManager.GetActiveScene().name);
    }
}
