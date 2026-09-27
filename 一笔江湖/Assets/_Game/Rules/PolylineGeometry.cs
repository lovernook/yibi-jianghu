using System;
using System.Collections.Generic;

namespace Yibi.Rules
{
    public static class PolylineGeometry
    {
        public const double Epsilon = 1e-9;
        public static double PointSegmentDistance(Point2 p, Point2 a, Point2 b)
        {
            double dx=b.X-a.X, dy=b.Y-a.Y, length2=dx*dx+dy*dy;
            if (length2 < Epsilon*Epsilon) return Point2.Distance(p,a);
            double t=((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length2;
            return Point2.Distance(p,Point2.Lerp(a,b,Math.Max(0,Math.Min(1,t))));
        }

        // Earliest intersection on a segment, including a start already inside the circle.
        public static bool CircleEntry(Point2 a, Point2 b, Point2 center, double radius, out double t)
        {
            t=0;
            double fx=a.X-center.X, fy=a.Y-center.Y;
            if (fx*fx+fy*fy <= radius*radius+Epsilon) return true;
            double dx=b.X-a.X, dy=b.Y-a.Y, aa=dx*dx+dy*dy;
            if (aa < Epsilon*Epsilon) return false;
            double bb=2*(fx*dx+fy*dy), cc=fx*fx+fy*fy-radius*radius;
            double disc=bb*bb-4*aa*cc;
            if (disc < -Epsilon) return false;
            t=(-bb-Math.Sqrt(Math.Max(0,disc)))/(2*aa);
            return t >= -Epsilon && t <= 1+Epsilon;
        }

        public static double Length(IList<Point2> line)
        { double result=0; for(int i=1;i<line.Count;i++) result+=Point2.Distance(line[i-1],line[i]); return result; }

        public static Point2[] Resample(IList<Point2> line, int count=64)
        {
            var result=new Point2[count];
            double length=Length(line), traversed=0;
            int segment=1;
            for(int i=0;i<count;i++)
            {
                double target=length*i/(count-1);
                while(segment < line.Count-1 && traversed+Point2.Distance(line[segment-1],line[segment]) < target)
                { traversed+=Point2.Distance(line[segment-1],line[segment]); segment++; }
                double d=Point2.Distance(line[segment-1],line[segment]);
                result[i]=Point2.Lerp(line[segment-1],line[segment],d<Epsilon?0:Math.Max(0,Math.Min(1,(target-traversed)/d)));
            }
            return result;
        }

        public static double DistanceToLine(Point2 p, IList<Point2> line)
        { double best=double.MaxValue; for(int i=1;i<line.Count;i++) best=Math.Min(best,PointSegmentDistance(p,line[i-1],line[i])); return best; }

        private static double Cross(Point2 a,Point2 b,Point2 c) => (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        public static bool SegmentsIntersect(Point2 a,Point2 b,Point2 c,Point2 d)
        {
            if (PointSegmentDistance(a,c,d)<Epsilon || PointSegmentDistance(b,c,d)<Epsilon ||
                PointSegmentDistance(c,a,b)<Epsilon || PointSegmentDistance(d,a,b)<Epsilon) return true;
            return Cross(a,b,c)*Cross(a,b,d)<0 && Cross(c,d,a)*Cross(c,d,b)<0;
        }

        public static string Validate(GestureTemplate template)
        {
            if(template==null || string.IsNullOrWhiteSpace(template.Id) || template.Nodes==null || template.Nodes.Length<4 || template.Nodes.Length>7)
                return "路线必须有稳定 ID 和 4—7 个节点。";
            if(double.IsNaN(template.Radius) || double.IsInfinity(template.Radius) || template.Radius<=0 || template.Radius>.15)
                return "节点半径应大于 0 且不超过 0.15。";
            var n=template.Nodes;
            for(int i=0;i<n.Length;i++)
            {
                if(!n[i].IsFinite || n[i].X<template.Radius || n[i].Y<template.Radius || n[i].X>1-template.Radius || n[i].Y>1-template.Radius)
                    return "节点超出画板内边距："+(i+1);
                for(int j=0;j<i;j++) if(Point2.Distance(n[i],n[j])<=2*template.Radius) return "节点圆重叠："+(j+1)+" / "+(i+1);
                if(i>0)
                    for(int j=0;j<n.Length;j++)
                        if(j!=i && j!=i-1 && PointSegmentDistance(n[j],n[i-1],n[i])<=template.Radius)
                            return "连接线穿过非相邻穴位："+(j+1);
            }
            for(int i=1;i<n.Length;i++)
                for(int j=i+2;j<n.Length;j++)
                    if(SegmentsIntersect(n[i-1],n[i],n[j-1],n[j])) return "路线不能自交或闭环。";
            return null;
        }
    }
}
