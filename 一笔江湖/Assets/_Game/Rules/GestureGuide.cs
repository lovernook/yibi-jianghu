using System;

namespace Yibi.Rules
{
    /// <summary>Incremental, allocation-free after Reset. Guidance only; GestureScorer remains authoritative.</summary>
    public sealed class GestureGuide
    {
        private GestureTemplate template;
        private bool[] inside;
        private int[] hits;
        private double[] times;
        private Point2 previous;
        private bool started;
        public int Reached { get; private set; }
        public GestureError Error { get; private set; }
        public int WrongNode { get; private set; }
        public double Distance { get; private set; }
        public void Reset(GestureTemplate route)
        {
            template=route;inside=new bool[route.Nodes.Length];hits=new int[route.Nodes.Length];times=new double[route.Nodes.Length];
            Reached=0;Error=GestureError.None;WrongNode=-1;Distance=0;started=false;
        }
        public void Push(QuantizedPoint point)
        {
            var p=point.ToPoint();Distance=PolylineGeometry.DistanceToLine(p,template.Nodes);
            if(Error!=GestureError.None)return;
            if(!started){started=true;previous=p;if(Point2.Distance(p,template.Nodes[0])>template.Radius){Fail(GestureError.WrongStart,0);return;}Reached=1;inside[0]=true;return;}
            int count=0;
            for(int node=0;node<template.Nodes.Length;node++){
                double t;if(!inside[node]&&PolylineGeometry.CircleEntry(previous,p,template.Nodes[node],template.Radius,out t)){
                    int index=count++;while(index>0&&times[index-1]>t){times[index]=times[index-1];hits[index]=hits[index-1];index--;}
                    times[index]=t;hits[index]=node;
                }
                inside[node]=Point2.Distance(p,template.Nodes[node])<=template.Radius+PolylineGeometry.Epsilon;
            }
            previous=p;
            for(int i=0;i<count;i++){int node=hits[i];if(node==Reached-1)continue;if(node>Reached){Fail(GestureError.MissingNode,Reached);return;}if(node<Reached){Fail(GestureError.WrongOrder,node);return;}Reached++;}
        }
        void Fail(GestureError error,int node){Error=error;WrongNode=node;}
    }
}
