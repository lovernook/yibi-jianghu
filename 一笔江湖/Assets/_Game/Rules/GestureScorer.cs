using System;
using System.Collections.Generic;

namespace Yibi.Rules
{
    public static class GestureScorer
    {
        public const int AlgorithmVersion=1;
        public const int MaxPoints=512;
        private struct Visit { public int Node; public double T; }

        public static ScoreResult Score(GestureTemplate template, IList<QuantizedPoint> points, bool outOfBounds=false, bool overflow=false)
        {
            if(PolylineGeometry.Validate(template)!=null) return ScoreResult.Invalid(GestureError.InvalidTemplate);
            if(overflow || (points!=null && points.Count>MaxPoints)) return ScoreResult.Invalid(GestureError.TooManyPoints);
            if(outOfBounds) return ScoreResult.Invalid(GestureError.OutOfBounds);
            if(points==null || points.Count<2) return ScoreResult.Invalid(GestureError.TooShort);
            var line=new List<Point2>(points.Count);
            for(int i=0;i<points.Count;i++)
            {
                if(points[i].x<0 || points[i].x>10000 || points[i].y<0 || points[i].y>10000) return ScoreResult.Invalid(GestureError.OutOfBounds);
                var p=points[i].ToPoint();
                if(line.Count==0 || Point2.Distance(p,line[line.Count-1])>PolylineGeometry.Epsilon) line.Add(p);
            }
            if(line.Count<2 || PolylineGeometry.Length(line)<1e-6) return ScoreResult.Invalid(GestureError.TooShort);
            var nodes=template.Nodes;
            if(Point2.Distance(line[0],nodes[0])>template.Radius) return ScoreResult.Invalid(GestureError.WrongStart,0);
            if(Point2.Distance(line[line.Count-1],nodes[nodes.Length-1])>template.Radius) return ScoreResult.Invalid(GestureError.WrongEnd,nodes.Length-1);

            int expected=0;
            var events=new List<Visit>(nodes.Length);
            var inside=new bool[nodes.Length];
            for(int segment=1;segment<line.Count;segment++)
            {
                events.Clear();
                for(int node=0;node<nodes.Length;node++)
                {
                    double t;
                    if(!inside[node] && PolylineGeometry.CircleEntry(line[segment-1],line[segment],nodes[node],template.Radius,out t))
                        events.Add(new Visit {Node=node,T=t});
                    inside[node]=Point2.Distance(line[segment],nodes[node])<=template.Radius+PolylineGeometry.Epsilon;
                }
                events.Sort((a,b)=>a.T.CompareTo(b.T));
                foreach(var visit in events)
                {
                    // Merge consecutive visits to the same node (e.g. jitter at its circle edge).
                    // Returning to an older node after visiting the next one is still WrongOrder.
                    if(visit.Node==expected-1)continue;
                    if(visit.Node>expected) return ScoreResult.Invalid(GestureError.MissingNode,expected);
                    if(visit.Node<expected) return ScoreResult.Invalid(GestureError.WrongOrder,visit.Node);
                    expected++;
                }
            }
            if(expected!=nodes.Length) return ScoreResult.Invalid(GestureError.MissingNode,expected);
            var player=PolylineGeometry.Resample(line);
            var target=PolylineGeometry.Resample(nodes);
            double dp=0,dt=0,order=0;
            for(int i=0;i<64;i++)
            {
                dp+=PolylineGeometry.DistanceToLine(player[i],nodes);
                dt+=PolylineGeometry.DistanceToLine(target[i],line);
                order+=Point2.Distance(player[i],target[i]);
            }
            dp/=64; dt/=64; order/=64;
            double lp=PolylineGeometry.Length(line),lt=PolylineGeometry.Length(nodes);
            double shape=Clamp(1-(dp+dt)/2/.08),qualityOrder=Clamp(1-order/.12),length=Math.Min(lp,lt)/Math.Max(lp,lt);
            return new ScoreResult { Valid=true, Score=(int)Math.Round(100*(.60*shape+.25*qualityOrder+.15*length),MidpointRounding.AwayFromZero),
                ShapeScore=100*shape,OrderScore=100*qualityOrder,LengthScore=100*length,PlayerToTemplate=dp,TemplateToPlayer=dt,
                PairedDistance=order,PlayerLength=lp,TemplateLength=lt,ErrorCode=GestureError.None };
        }
        private static double Clamp(double value)=>Math.Max(0,Math.Min(1,value));
    }
}
