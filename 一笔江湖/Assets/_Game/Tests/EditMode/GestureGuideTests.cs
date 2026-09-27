using NUnit.Framework;
using Yibi.Rules;
using Yibi.Battle;
using UnityEngine;

public class GestureGuideTests
{
    [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
    public void SparseAndDenseGuidanceAgreeWithValidScoring(int routeIndex)
    {
        var route=BattleContent.Routes()[routeIndex];
        foreach(int density in new[]{1,12,40}){var points=FixedGestureSamples.Dense(route,density);var guide=new GestureGuide();guide.Reset(route);foreach(var p in points)guide.Push(p);Assert.IsTrue(GestureScorer.Score(route,points).Valid);Assert.AreEqual(GestureError.None,guide.Error);Assert.AreEqual(route.Nodes.Length,guide.Reached);}
    }
    [Test] public void ShortcutAndReverseReportTheNodeThatNeedsCorrection()
    {
        var route=StarterRoutes.Create()[0];var guide=new GestureGuide();guide.Reset(route);guide.Push(QuantizedPoint.From(route.Nodes[0]));guide.Push(QuantizedPoint.From(route.Nodes[2]));Assert.AreEqual(GestureError.MissingNode,guide.Error);Assert.AreEqual(1,guide.WrongNode);
        guide.Reset(route);guide.Push(QuantizedPoint.From(route.Nodes[0]));guide.Push(QuantizedPoint.From(route.Nodes[1]));guide.Push(QuantizedPoint.From(route.Nodes[0]));Assert.AreEqual(GestureError.WrongOrder,guide.Error);Assert.AreEqual(0,guide.WrongNode);
    }
    [Test] public void OldProfilesMigratePracticeFieldAndRoundTripRecords()
    {
        var profile=new PlayerProfile{practice=null};ProfileStore.Validate(profile);Assert.IsNotNull(profile.practice);
        using(ProfileStore.UseTransientProfile(profile)){ProfileStore.RecordPractice("dianxue",91);ProfileStore.RecordPractice("dianxue",74);var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));ProfileStore.Validate(copy);Assert.AreEqual(2,copy.practice[0].attempts);Assert.AreEqual(91,copy.practice[0].bestScore);Assert.AreEqual(74,copy.practice[0].lastScore);Assert.AreEqual(3,profile.tickets,"Initial 2 + practice reward 1, once");}
    }
    [Test] public void PracticeRejectsUnknownRouteInsteadOfPollutingSave()
    {
        using(ProfileStore.UseTransientProfile(new PlayerProfile()))Assert.Throws<System.ArgumentException>(()=>ProfileStore.RecordPractice("unknown",90));
    }
}
