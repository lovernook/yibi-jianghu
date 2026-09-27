using System;
using System.Collections.Generic;

namespace Yibi.Rules
{
    // Pure state machine: Unity passes board coordinates and an external clock.
    public sealed class GestureSampler
    {
        private readonly List<QuantizedPoint> points=new List<QuantizedPoint>(GestureScorer.MaxPoints);
        private Point2 last;
        private double lastTime;
        public IReadOnlyList<QuantizedPoint> Points=>points;
        public bool IsDrawing { get; private set; }
        public bool OutOfBounds { get; private set; }
        public bool Overflow { get; private set; }
        public double MinimumDistance=.003;
        public double MaximumInterval=.033;
        public void Clear() { points.Clear(); IsDrawing=false; OutOfBounds=false; Overflow=false; }
        public void Begin(Point2 p,double time) { Clear(); IsDrawing=true; Add(p,time,true); }
        public void Move(Point2 p,double time) { if(IsDrawing) Add(p,time,false); }
        public void End(Point2 p,double time) { if(!IsDrawing)return; Add(p,time,true); IsDrawing=false; }
        public QuantizedPoint[] Snapshot()=>points.ToArray();
        private void Add(Point2 p,double time,bool force)
        {
            if(!p.IsFinite || double.IsNaN(time) || double.IsInfinity(time)) return;
            if(p.X<0 || p.X>1 || p.Y<0 || p.Y>1) { OutOfBounds=true; return; }
            if(Overflow) return;
            if(!force && points.Count>0 && Point2.Distance(last,p)<MinimumDistance && time-lastTime<MaximumInterval) return;
            var q=QuantizedPoint.From(p);
            if(points.Count==GestureScorer.MaxPoints)
            {
                // Identical release position needs no extra slot; a new endpoint would overflow.
                if(force && q.x==points[points.Count-1].x && q.y==points[points.Count-1].y) return;
                Overflow=true; return;
            }
            points.Add(q); last=p; lastTime=time;
        }
    }
}
