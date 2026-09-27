using System;
using System.IO;
using NUnit.Framework;
using Yibi.Rules;
using Yibi.Networking;

public class NetworkContractTests
{
    private sealed class Fragmented:MemoryStream
    {public Fragmented(byte[] bytes):base(bytes){} public override int Read(byte[] buffer,int offset,int count)=>base.Read(buffer,offset,Math.Min(1,count));}
    [Test] public void PartialReadsAndCoalescedFrames(){var a=TcpFraming.Encode("{\"字\":1}");var b=TcpFraming.Encode("{}");var both=new byte[a.Length+b.Length];Array.Copy(a,both,a.Length);Array.Copy(b,0,both,a.Length,b.Length);using(var s=new Fragmented(both)){Assert.AreEqual("{\"字\":1}",TcpFraming.Read(s));Assert.AreEqual("{}",TcpFraming.Read(s));}}
    [Test] public void InvalidLengthsAndTruncationRejected(){Assert.Throws<InvalidDataException>(()=>TcpFraming.Read(new MemoryStream(new byte[4])));Assert.Throws<InvalidDataException>(()=>TcpFraming.Read(new MemoryStream(new byte[]{0,1,0,1})));Assert.Throws<EndOfStreamException>(()=>TcpFraming.Read(new MemoryStream(new byte[]{0,0,0,2,65})));}
    [Test] public void CatalogHashStableAndSensitive(){var a=ContentCatalog.Default();var b=ContentCatalog.Default();Assert.AreEqual(a.Hash(),b.Hash());b.routes[0].cost=1;Assert.AreNotEqual(a.Hash(),b.Hash());}
    [Test] public void CatalogRejectsWrongIdsAndNegativeCosts(){var a=ContentCatalog.Default();a.routes[0].id="lieshi";Assert.Throws<ArgumentException>(()=>a.Validate());a=ContentCatalog.Default();a.routes[0].cost=-1;Assert.Throws<ArgumentException>(()=>a.Validate());}
    [Test] public void WindBurnShieldAndCleanseOrder()
    {
        var s=BattleReducer.Create("t",0,new[]{"huifeng","liuhuo","qingxin"},new[]{"huti","qingxin","dianxue"});s.fighters[0].energy=6;
        s=Act(s,"huifeng");s=Act(s,"huti");s=Act(s,"liuhuo");Assert.AreEqual(1,s.fighters[1].burn,"first burn tick at own turn start");s=Act(s,"qingxin");Assert.AreEqual(0,s.fighters[1].burn);
    }
    [Test] public void CounterCannotRecursivelyTriggerFromBurn()
    {var s=BattleReducer.Create("t",0);s.fighters[1].burn=1;s.fighters[1].shield=10;s.fighters[1].shieldUntil=4;s=Act(s,"rest");Assert.AreEqual(-1,s.fighters[1].counterUntil);Assert.AreEqual(7,s.fighters[1].shield);}
    private BattleState Act(BattleState s,string skill){var r=BattleReducer.Apply(s,new BattleCommand{seat=s.activeSeat,matchId=s.matchId,turnId=s.turnId,skillId=skill,points=skill=="rest"?Array.Empty<QuantizedPoint>():BattleContent.Perfect(skill)});Assert.IsTrue(r.Accepted,r.rejection);return r.state;}
}
