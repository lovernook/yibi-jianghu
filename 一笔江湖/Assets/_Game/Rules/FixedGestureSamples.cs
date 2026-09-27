using System;
using System.Collections.Generic;

namespace Yibi.Rules
{
    public sealed class GestureSample
    {
        public string Name;
        public QuantizedPoint[] Points;
        public bool ExpectedValid;
        public GestureError ExpectedError;
        public int MinimumScore, MaximumScore;
        public GestureSample(string name,QuantizedPoint[] points,bool valid,GestureError error=GestureError.None,int min=0,int max=100)
        {Name=name;Points=points;ExpectedValid=valid;ExpectedError=error;MinimumScore=min;MaximumScore=max;}
    }

    // Deterministic fixtures; these do not replace saved human mouse traces.
    public static class FixedGestureSamples
    {
        public static QuantizedPoint[] Dense(GestureTemplate template,int steps)
        {
            var points=new List<QuantizedPoint>();
            for(int i=1;i<template.Nodes.Length;i++)
                for(int j=0;j<steps;j++) points.Add(QuantizedPoint.From(Point2.Lerp(template.Nodes[i-1],template.Nodes[i],j/(double)steps)));
            points.Add(QuantizedPoint.From(template.Nodes[template.Nodes.Length-1]));
            return points.ToArray();
        }
        public static GestureSample[] Create(GestureTemplate t)
        {
            var result=new List<GestureSample>();
            var sparse=Dense(t,1); var dense=Dense(t,40);
            result.Add(new GestureSample("标准稀疏",sparse,true,min:99));
            result.Add(new GestureSample("标准密集",dense,true,min:99));
            result.Add(new GestureSample("标准中密度",Dense(t,7),true,min:99));
            var jitter=(QuantizedPoint[])dense.Clone();
            for(int i=1;i<jitter.Length-1;i++) {jitter[i].x+=(int)(40*Math.Sin(i*1.37));jitter[i].y+=(int)(40*Math.Cos(i*1.81));}
            result.Add(new GestureSample("轻微手抖",jitter,true,min:85));
            var offset=(QuantizedPoint[])dense.Clone(); for(int i=0;i<offset.Length;i++) offset[i].x+=100;
            result.Add(new GestureSample("少量偏移",offset,true,min:80));
            var reverse=(QuantizedPoint[])sparse.Clone(); Array.Reverse(reverse);
            result.Add(new GestureSample("逆序",reverse,false,GestureError.WrongStart));
            var missing=new List<QuantizedPoint>(sparse); missing.RemoveAt(2);
            result.Add(new GestureSample("漏节点",missing.ToArray(),false,GestureError.MissingNode));
            result.Add(new GestureSample("捷径跳点",new[]{sparse[0],sparse[sparse.Length-1]},false,GestureError.MissingNode));
            var duplicate=new List<QuantizedPoint>(); foreach(var p in sparse){duplicate.Add(p);duplicate.Add(p);duplicate.Add(p);}
            result.Add(new GestureSample("重复点",duplicate.ToArray(),true,min:99));
            result.Add(new GestureSample("单点",new[]{sparse[0]},false,GestureError.TooShort));
            var outside=(QuantizedPoint[])dense.Clone();outside[2].x=11000;
            result.Add(new GestureSample("越界",outside,false,GestureError.OutOfBounds));
            var wrongEnd=(QuantizedPoint[])dense.Clone(); wrongEnd[wrongEnd.Length-1]=sparse[0];
            result.Add(new GestureSample("错误终点",wrongEnd,false,GestureError.WrongEnd));
            var backward=new List<QuantizedPoint>{sparse[0],sparse[1],sparse[0]}; backward.AddRange(sparse);
            result.Add(new GestureSample("逆序返回",backward.ToArray(),false,GestureError.WrongOrder));
            var tooMany=new QuantizedPoint[513]; for(int i=0;i<513;i++)tooMany[i]=sparse[0];
            result.Add(new GestureSample("超采样上限",tooMany,false,GestureError.TooManyPoints));
            // Excursion between node 1 and 2, away from their circles. Nodes still visited in order.
            var detour=new List<QuantizedPoint>{sparse[0]};
            var a=t.Nodes[0];var b=t.Nodes[1];
            detour.Add(QuantizedPoint.From(Point2.Lerp(a,b,.25)));
            detour.Add(QuantizedPoint.From(new Point2(Point2.Lerp(a,b,.25).X,.055)));
            detour.Add(QuantizedPoint.From(new Point2(Point2.Lerp(a,b,.75).X,.055)));
            detour.Add(QuantizedPoint.From(Point2.Lerp(a,b,.75)));
            for(int i=1;i<sparse.Length;i++)detour.Add(sparse[i]);
            result.Add(new GestureSample("大幅绕行",detour.ToArray(),true,max:95));
            return result.ToArray();
        }
    }
}
