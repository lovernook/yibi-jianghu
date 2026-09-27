using NUnit.Framework;
using Yibi.Rules;

public class BattleRulesTests
{
    private BattleTransition Do(BattleState s,string skill,bool timeout=false,QuantizedPoint[] points=null)
    {return BattleReducer.Apply(s,new BattleCommand{seat=s.activeSeat,matchId=s.matchId,turnId=s.turnId,skillId=skill,timeout=timeout,points=points??(skill=="basic"||skill=="rest"?new QuantizedPoint[0]:BattleContent.Perfect(skill))});}
    [Test] public void DamageConsumesShieldAndPreservesInput(){var s=BattleReducer.Create("t",0);var r=Do(s,"basic");Assert.AreEqual(99,r.state.fighters[1].hp);Assert.AreEqual(100,s.fighters[1].hp);Assert.AreEqual(1,r.state.activeSeat);}
    [Test] public void FlawComboExpiresAfterNextAction(){var s=BattleReducer.Create("t",0);s.fighters[0].energy=6;var a=Do(s,"dianxue");var b=Do(a.state,"rest");var c=Do(b.state,"lieshi");Assert.IsTrue(c.Accepted);Assert.Contains("破绽联动 +8",c.events);Assert.AreEqual(-1,c.state.fighters[1].flawUntil);}
    [Test] public void CooldownBlocksFollowingOwnTurn(){var s=BattleReducer.Create("t",0);s.fighters[0].energy=6;s=Do(Do(s,"dianxue").state,"rest").state;Assert.AreEqual("秘籍冷却中",Do(s,"dianxue").rejection);}
    [Test] public void InvalidGestureConsumesTurnWithoutCost(){var s=BattleReducer.Create("t",0);var r=Do(s,"dianxue",false,new QuantizedPoint[0]);Assert.IsTrue(r.Accepted);Assert.IsFalse(r.score.Valid);Assert.AreEqual(3,r.state.fighters[0].energy);Assert.AreEqual(0,r.state.fighters[0].eligible[0]);Assert.AreEqual(2,r.state.turnId);}
    [Test] public void TimeoutsLoseAndDoNotGrantMindsetShield(){var s=BattleReducer.Create("t",0);s=Do(s,"rest",true).state;Assert.AreEqual(0,s.fighters[0].shield);s=Do(s,"rest").state;s=Do(s,"rest",true).state;Assert.AreEqual(1,s.winner);}
    [Test] public void StaleCommandCannotChangeState(){var s=BattleReducer.Create("t",0);var r=BattleReducer.Apply(s,new BattleCommand{seat=0,matchId="t",turnId=0,skillId="basic"});Assert.AreEqual("过期行动",r.rejection);Assert.AreSame(s,r.state);}
    [Test] public void AllSixRoutesAreValid(){foreach(var t in BattleContent.Routes())Assert.IsTrue(GestureScorer.Score(t,BattleContent.Perfect(t.Id)).Valid,t.Id);}
    [Test] public void EveryMatchTerminatesWithinFortyActions(){for(int first=0;first<2;first++){var s=BattleReducer.Create("t",first);for(int i=0;i<40&&s.winner==-2;i++)s=Do(s,"rest").state;Assert.AreEqual(-1,s.winner);}}
}
