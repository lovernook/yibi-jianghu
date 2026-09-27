using System.Collections.Generic;
using NUnit.Framework;
using Yibi.Rules;

public class GestureRuleTests
{
    public static IEnumerable<TestCaseData> Samples()
    {
        foreach(var t in StarterRoutes.Create())
            foreach(var sample in FixedGestureSamples.Create(t))
                yield return new TestCaseData(t,sample).SetName(t.Id+"_"+sample.Name);
    }

    [TestCaseSource(nameof(Samples))]
    public void FixedSample(GestureTemplate template,GestureSample sample)
    {
        Assert.That(PolylineGeometry.Validate(template),Is.Null);
        var a=GestureScorer.Score(template,sample.Points);
        Assert.That(a.Valid,Is.EqualTo(sample.ExpectedValid),a.ErrorCode.ToString());
        Assert.That(a.ErrorCode,Is.EqualTo(sample.ExpectedError));
        if(a.Valid) Assert.That(a.Score,Is.InRange(sample.MinimumScore,sample.MaximumScore));
        var b=GestureScorer.Score(template,sample.Points);
        Assert.That(b.Score,Is.EqualTo(a.Score));
        Assert.That(b.ShapeScore,Is.EqualTo(a.ShapeScore));
        Assert.That(b.ErrorCode,Is.EqualTo(a.ErrorCode));
    }

    [Test]
    public void DensityDoesNotChangeQuality()
    {
        foreach(var t in StarterRoutes.Create())
        {
            int sparse=GestureScorer.Score(t,FixedGestureSamples.Dense(t,1)).Score;
            int dense=GestureScorer.Score(t,FixedGestureSamples.Dense(t,70)).Score;
            Assert.That(System.Math.Abs(sparse-dense),Is.LessThanOrEqualTo(2));
        }
    }

    [Test]
    public void SegmentCircleDetectsNodeBetweenSamplesAndOrdersByIntersection()
    {
        double entry;
        Assert.That(PolylineGeometry.CircleEntry(new Point2(0,0),new Point2(1,0),new Point2(.5,0),.1,out entry),Is.True);
        Assert.That(entry,Is.EqualTo(.4).Within(1e-8));
        var t=new GestureTemplate("straight",new[]{new Point2(.1,.5),new Point2(.3,.5),new Point2(.6,.5),new Point2(.9,.5)});
        Assert.That(GestureScorer.Score(t,new[]{new QuantizedPoint(1000,5000),new QuantizedPoint(9000,5000)}).Score,Is.EqualTo(100));
    }

    [Test]
    public void ProjectionAndDegenerateSegments()
    {
        Assert.That(PolylineGeometry.PointSegmentDistance(new Point2(2,1),new Point2(0,0),new Point2(1,0)),Is.EqualTo(System.Math.Sqrt(2)).Within(1e-9));
        Assert.That(PolylineGeometry.PointSegmentDistance(new Point2(0,1),new Point2(0,0),new Point2(0,0)),Is.EqualTo(1));
    }

    [Test]
    public void OverlapAndSelfIntersectionAreRejected()
    {
        var overlap=new GestureTemplate("bad",new[]{new Point2(.2,.2),new Point2(.21,.21),new Point2(.5,.5),new Point2(.8,.8)});
        Assert.That(PolylineGeometry.Validate(overlap),Is.Not.Null);
        var cross=new GestureTemplate("cross",new[]{new Point2(.2,.2),new Point2(.8,.8),new Point2(.2,.8),new Point2(.8,.2)});
        Assert.That(PolylineGeometry.Validate(cross),Is.Not.Null);
    }

    [Test]
    public void SamplerSingleStrokeQuantizationTimeDistanceAndEndpoint()
    {
        var s=new GestureSampler();s.Begin(new Point2(.1,.1),0);
        s.Move(new Point2(.1001,.1),.01);Assert.That(s.Points.Count,Is.EqualTo(1));
        s.Move(new Point2(.1001,.1),.04);Assert.That(s.Points.Count,Is.EqualTo(2));
        s.Move(new Point2(.2,.1),.041);Assert.That(s.Points.Count,Is.EqualTo(3));
        s.End(new Point2(.2001,.1),.042);Assert.That(s.Points.Count,Is.EqualTo(4));
        Assert.That(s.Points[3].x,Is.EqualTo(2001));Assert.That(s.IsDrawing,Is.False);
        s.Begin(new Point2(.3,.3),1);Assert.That(s.Points.Count,Is.EqualTo(1));
    }

    [Test]
    public void CircleEdgeJitterMergesConsecutiveNodeVisits()
    {
        var t=StarterRoutes.Create()[0];var p=new List<QuantizedPoint>{QuantizedPoint.From(t.Nodes[0]),new QuantizedPoint(3000,1600),new QuantizedPoint(2900,1600)};
        p.AddRange(FixedGestureSamples.Dense(t,20));
        Assert.That(GestureScorer.Score(t,p).Valid,Is.True);
    }

    [Test]
    public void SamplerBoundsNonfiniteAndCapacity()
    {
        var s=new GestureSampler();s.Begin(new Point2(.1,.1),0);
        s.Move(new Point2(double.NaN,0),1);Assert.That(s.Points.Count,Is.EqualTo(1));
        s.Move(new Point2(1.2,.3),2);Assert.That(s.OutOfBounds,Is.True);
        Assert.That(s.Points.Count,Is.EqualTo(1));
        s.Clear();s.Begin(new Point2(.2,.2),0);
        for(int i=1;i<600;i++)s.Move(new Point2(.2+(i%2)*.1,.2),i*.04);
        Assert.That(s.Points.Count,Is.EqualTo(512));Assert.That(s.Overflow,Is.True);
    }
}
