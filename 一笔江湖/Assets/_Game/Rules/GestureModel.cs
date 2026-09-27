using System;

namespace Yibi.Rules
{
    public struct Point2
    {
        public readonly double X, Y;
        public Point2(double x, double y) { X = x; Y = y; }
        public static Point2 Lerp(Point2 a, Point2 b, double t) => new Point2(a.X + (b.X-a.X)*t, a.Y + (b.Y-a.Y)*t);
        public static double Distance(Point2 a, Point2 b) { double x=a.X-b.X, y=a.Y-b.Y; return Math.Sqrt(x*x+y*y); }
        public bool IsFinite => !double.IsNaN(X) && !double.IsInfinity(X) && !double.IsNaN(Y) && !double.IsInfinity(Y);
    }

    [Serializable]
    public struct QuantizedPoint
    {
        public int x, y;
        public QuantizedPoint(int x, int y) { this.x=x; this.y=y; }
        public Point2 ToPoint() => new Point2(x/10000.0, y/10000.0);
        public static QuantizedPoint From(Point2 p) => new QuantizedPoint(
            (int)Math.Round(p.X*10000, MidpointRounding.AwayFromZero),
            (int)Math.Round(p.Y*10000, MidpointRounding.AwayFromZero));
    }

    public sealed class GestureTemplate
    {
        public readonly string Id;
        public readonly Point2[] Nodes;
        public readonly double Radius;
        public GestureTemplate(string id, Point2[] nodes, double radius=0.045)
        { Id=id; Nodes=nodes == null ? null : (Point2[])nodes.Clone(); Radius=radius; }
    }

    public enum GestureError { None, TooShort, OutOfBounds, WrongStart, WrongEnd, MissingNode, WrongOrder, TooManyPoints, InvalidTemplate }

    public sealed class ScoreResult
    {
        public bool Valid;
        public int Score;
        public double ShapeScore, OrderScore, LengthScore;
        public double PlayerToTemplate, TemplateToPlayer, PairedDistance, PlayerLength, TemplateLength;
        public GestureError ErrorCode;
        // Zero-based; UI adds one. -1 means no individual node caused the error.
        public int FirstWrongNode = -1;
        public double Multiplier => Valid ? 0.8+0.4*Score/100.0 : 0;
        public static ScoreResult Invalid(GestureError error, int node=-1) => new ScoreResult { ErrorCode=error, FirstWrongNode=node };
    }

    public static class StarterRoutes
    {
        public static GestureTemplate[] Create() => new[] {
            new GestureTemplate("dianxue", new[] { new Point2(.25,.16), new Point2(.68,.36), new Point2(.32,.62), new Point2(.72,.84) }),
            new GestureTemplate("lieshi", new[] { new Point2(.2,.16), new Point2(.72,.3), new Point2(.28,.48), new Point2(.74,.68), new Point2(.4,.86) }),
            new GestureTemplate("huifeng", new[] { new Point2(.2,.17), new Point2(.47,.25), new Point2(.76,.39), new Point2(.72,.6), new Point2(.48,.78), new Point2(.22,.85) })
        };
    }
}
