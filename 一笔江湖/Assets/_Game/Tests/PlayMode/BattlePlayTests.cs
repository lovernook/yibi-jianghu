using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yibi.Battle;
using Yibi.Rules;

public class BattlePlayTests
{
    private System.IDisposable profile;
    [SetUp] public void IsolateProfile(){profile=ProfileStore.UseTransientProfile(new PlayerProfile());}
    [UnityTearDown] public IEnumerator RestoreProfile(){yield return SceneManager.LoadSceneAsync("Bootstrap");profile.Dispose();}
    [UnityTest] public IEnumerator DrawConfirmNpcAndRestart()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone");yield return null;
        var p=Object.FindObjectOfType<BattlePresenter>();Assert.IsNotNull(p);p.SelectSkill(0);Assert.IsTrue(p.drawingPanel.activeSelf);
        var nodes=p.board.template.nodes;p.board.Begin(new Point2(nodes[0].x,nodes[0].y),0);
        for(int i=1;i<nodes.Length-1;i++)p.board.Move(new Point2(nodes[i].x,nodes[i].y),i*.1);
        var end=nodes[nodes.Length-1];p.board.Finish(new Point2(end.x,end.y),1);
        Assert.AreEqual(100,p.board.LastResult.Score);p.confirmButton.onClick.Invoke();int rev=p.State.revision;p.confirmButton.onClick.Invoke();Assert.AreEqual(rev,p.State.revision,"duplicate confirm");
        Assert.AreEqual(1,p.State.fighters[0].energy);Assert.AreEqual(95,p.State.fighters[1].hp);
        float npcWait=(p.presentation==null?2.1f:p.presentation.actionDuration+p.presentation.npcThinkDelay)+1f;
        float npcDeadline=Time.realtimeSinceStartup+Mathf.Clamp(npcWait,1f,10f);
        while(p.State.turnId<3&&Time.realtimeSinceStartup<npcDeadline)yield return null;
        Assert.AreEqual(3,p.State.turnId,"NPC must act within the configured presentation and thinking time budget");Assert.AreEqual(0,p.State.activeSeat);
        p.Restart();Assert.AreEqual(100,p.State.fighters[0].hp);Assert.IsFalse(p.resultPanel.activeSelf);Assert.AreEqual(1,p.State.turnId);
    }
    [UnityTest] public IEnumerator FullPveMatchReachesResult()
    {
        yield return SceneManager.LoadSceneAsync("Arena_Stone");yield return null;var p=Object.FindObjectOfType<BattlePresenter>();
        float deadline=Time.realtimeSinceStartup+65;
        while(p.State.winner==-2&&Time.realtimeSinceStartup<deadline){
            if(p.State.activeSeat==0&&p.basicButton.interactable){
                string skill=BattleReducer.CanUse(p.State,0,"lieshi")==null?"lieshi":BattleReducer.CanUse(p.State,0,"dianxue")==null?"dianxue":"basic";
                p.Resolve(skill,skill=="basic"?null:BattleContent.Perfect(skill));
            }
            yield return null;
        }
        Assert.AreNotEqual(-2,p.State.winner);
        float resultWait=(p.presentation==null ? .9f : p.presentation.actionDuration)+1f;
        float resultDeadline=Time.realtimeSinceStartup+Mathf.Clamp(resultWait,1f,10f);
        while(!p.resultPanel.activeSelf&&Time.realtimeSinceStartup<resultDeadline)yield return null;
        Assert.IsTrue(p.resultPanel.activeSelf,"The completed match must show its result after the configured final action presentation");Assert.IsFalse(p.basicButton.interactable);
    }
}
