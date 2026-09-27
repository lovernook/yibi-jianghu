using System;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;

namespace Yibi.Rules
{
    [Serializable] public sealed class RouteContent
    {public string id;public int cost;public int radius=450;public QuantizedPoint[] nodes;}
    [Serializable] public sealed class ContentCatalog
    {
        public static string InstalledHash {get;private set;}
        public int schemaVersion=1,gestureVersion=1;
        public RouteContent[] routes;
        public static ContentCatalog Default()
        {
            var templates=BattleContent.Routes();var c=new ContentCatalog{routes=new RouteContent[6]};
            for(int i=0;i<6;i++){var t=templates[i];var a=new QuantizedPoint[t.Nodes.Length];for(int n=0;n<a.Length;n++)a[n]=QuantizedPoint.From(t.Nodes[n]);c.routes[i]=new RouteContent{id=t.Id,cost=BattleContent.Costs[i],radius=(int)Math.Round(t.Radius*10000),nodes=a};}return c;
        }
        public void Validate()
        {
            if(schemaVersion!=1||gestureVersion!=1||routes==null||routes.Length!=6)throw new ArgumentException("Unsupported catalog");
            for(int i=0;i<6;i++){
                var r=routes[i];if(r==null||r.id!=BattleContent.Ids[i]||r.cost<0||r.cost>6||r.nodes==null)throw new ArgumentException("Invalid technique "+i);
                var t=Template(r);var result=GestureScorer.Score(t,r.nodes);if(!result.Valid)throw new ArgumentException("Invalid route "+r.id+": "+result.ErrorCode);
            }
        }
        public static GestureTemplate Template(RouteContent r){var nodes=new Point2[r.nodes.Length];for(int i=0;i<nodes.Length;i++)nodes[i]=r.nodes[i].ToPoint();return new GestureTemplate(r.id,nodes,r.radius/10000.0);}
        public string Canonical()
        {
            Validate();var b=new StringBuilder("yibi|1|gesture:1|rules:1\n");
            foreach(var r in routes){b.Append(r.id).Append('|').Append(r.cost.ToString(CultureInfo.InvariantCulture)).Append('|').Append(r.radius.ToString(CultureInfo.InvariantCulture));foreach(var p in r.nodes)b.Append('|').Append(p.x.ToString(CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString(CultureInfo.InvariantCulture));b.Append('\n');}return b.ToString();
        }
        public string Hash(){using(var sha=SHA256.Create()){var bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical()));return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}}
        public void Install(){Validate();var a=new GestureTemplate[6];for(int i=0;i<6;i++){a[i]=Template(routes[i]);BattleContent.Costs[i]=routes[i].cost;}BattleContent.InstalledRoutes=a;InstalledHash=Hash();}
    }
}
