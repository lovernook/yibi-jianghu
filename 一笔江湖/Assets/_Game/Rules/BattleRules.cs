using System;
using System.Collections.Generic;

namespace Yibi.Rules
{
    [Serializable] public sealed class FighterState
    {
        public int hp=100, energy=3, shield, shieldUntil=-1, ownTurn;
        public int flawUntil=-1, windUntil=-1, counterUntil=-1, burn, timeouts;
        public string mindset="shouzhuo";
        public string[] loadout={"dianxue","lieshi","huifeng"};
        public int[] eligible=new int[6];
        public FighterState Copy(){var f=(FighterState)MemberwiseClone();f.loadout=(string[])loadout.Clone();f.eligible=(int[])eligible.Clone();return f;}
    }
    [Serializable] public sealed class BattleState
    {
        public string matchId;
        public int revision, turnId=1, activeSeat, winner=-2;
        public FighterState[] fighters={new FighterState(),new FighterState()};
        public BattleState Copy(){var s=(BattleState)MemberwiseClone();s.fighters=new[]{fighters[0].Copy(),fighters[1].Copy()};return s;}
    }
    [Serializable] public sealed class BattleCommand
    {
        public string commandId, matchId, skillId;
        public int seat, turnId;
        public QuantizedPoint[] points=Array.Empty<QuantizedPoint>();
        public bool timeout, outOfBounds, overflow;
    }
    public sealed class BattleTransition
    {
        public BattleState state;
        public string rejection;
        public ScoreResult score;
        public List<string> events=new List<string>();
        public bool Accepted=>rejection==null;
    }
    public static class BattleContent
    {
        internal static GestureTemplate[] InstalledRoutes;
        public static readonly string[] Ids={"dianxue","lieshi","huifeng","liuhuo","huti","qingxin"};
        public static readonly string[] Names={"点穴指","裂石掌","回风诀","流火诀","护体功","清心诀"};
        public static readonly int[] Costs={2,3,2,3,2,2};
        public static GestureTemplate[] Routes()
        {
            if(InstalledRoutes!=null)return InstalledRoutes;
            var first=StarterRoutes.Create();return new[]{first[0],first[1],first[2],
                new GestureTemplate("liuhuo",new[]{new Point2(.2,.2),new Point2(.4,.42),new Point2(.72,.3),new Point2(.65,.65),new Point2(.3,.82)}),
                new GestureTemplate("huti",new[]{new Point2(.2,.2),new Point2(.2,.7),new Point2(.5,.85),new Point2(.8,.7),new Point2(.8,.2)}),
                new GestureTemplate("qingxin",new[]{new Point2(.2,.2),new Point2(.4,.35),new Point2(.25,.6),new Point2(.55,.8),new Point2(.8,.65)})};
        }
        public static string Name(string id){int i=Array.IndexOf(Ids,id);return i>=0?Names[i]:id=="rest"?"调息":"吐纳掌";}
        public static QuantizedPoint[] Perfect(string id){var route=Routes()[Array.IndexOf(Ids,id)];var a=new QuantizedPoint[route.Nodes.Length];for(int i=0;i<a.Length;i++)a[i]=QuantizedPoint.From(route.Nodes[i]);return a;}
    }
    // This reducer has no clock, Unity objects or networking. Callers own deadlines and identity.
    public static class BattleReducer
    {
        public static BattleState Create(string matchId,int firstSeat,string[] left=null,string[] right=null,string leftMind="shouzhuo",string rightMind="fanzhao")
        {
            if(firstSeat<0||firstSeat>1)throw new ArgumentOutOfRangeException(nameof(firstSeat));
            var s=new BattleState{matchId=matchId,activeSeat=firstSeat};
            if(left!=null)s.fighters[0].loadout=(string[])left.Clone();if(right!=null)s.fighters[1].loadout=(string[])right.Clone();
            s.fighters[0].mindset=leftMind;s.fighters[1].mindset=rightMind;
            s.fighters[1-firstSeat].shield=5;s.fighters[1-firstSeat].shieldUntil=int.MaxValue;
            s.fighters[firstSeat].ownTurn=1;return s;
        }
        public static string CanUse(BattleState s,int seat,string skill)
        {
            if(s.winner!=-2)return "对局已结束";if(seat!=s.activeSeat)return "尚未轮到你";
            if(skill=="basic"||skill=="rest")return null;
            int i=Array.IndexOf(BattleContent.Ids,skill);var f=s.fighters[seat];
            if(i<0||Array.IndexOf(f.loadout,skill)<0)return "未装备此秘籍";
            if(f.ownTurn<f.eligible[i])return "秘籍冷却中";
            return f.energy<BattleContent.Costs[i]?"内力不足":null;
        }
        public static BattleTransition Apply(BattleState source,BattleCommand c)
        {
            var r=new BattleTransition{state=source};
            if(c==null||c.seat<0||c.seat>1){r.rejection="无效席位";return r;}
            if(c.matchId!=source.matchId||c.turnId!=source.turnId){r.rejection="过期行动";return r;}
            r.rejection=CanUse(source,c.seat,c.skillId);if(r.rejection!=null)return r;
            if(c.timeout&&c.skillId!="rest"){r.rejection="超时只能调息";return r;}
            int index=Array.IndexOf(BattleContent.Ids,c.skillId);
            if(index>=0){
                if(c.points==null||c.points.Length>512){r.rejection="轨迹格式错误";return r;}
                foreach(var p in c.points)if(p.x<0||p.x>10000||p.y<0||p.y>10000){r.rejection="轨迹坐标越界";return r;}
                r.score=GestureScorer.Score(BattleContent.Routes()[index],c.points,c.outOfBounds,c.overflow);
            }
            var s=source.Copy();r.state=s;var a=s.fighters[c.seat];var b=s.fighters[1-c.seat];
            a.timeouts=c.timeout?a.timeouts+1:0;
            if(a.timeouts>=2){s.winner=1-c.seat;s.revision++;r.events.Add("连续两个个人回合超时，判负");return r;}
            bool valid=index<0||r.score.Valid;double mul=index<0?1:r.score.Multiplier;
            r.events.Add((c.seat==0?"我方":"对方")+" · "+BattleContent.Name(c.skillId)+(index<0?"":valid?" · "+r.score.Score+"分":" · 运功失误，改为吐纳掌"));
            int damage=0;
            if(!valid)damage=6;
            else if(c.skillId=="basic")damage=6;
            else if(c.skillId=="rest") {a.energy=Math.Min(6,a.energy+2);if(!c.timeout&&a.mindset=="shouzhuo")Shield(a,4);}
            else {
                a.energy-=BattleContent.Costs[index];a.eligible[index]=a.ownTurn+2;
                switch(c.skillId){
                    case "dianxue":damage=8;b.flawUntil=a.ownTurn+1;break;
                    case "lieshi":damage=18;if(b.flawUntil>=a.ownTurn){damage+=8;b.flawUntil=-1;r.events.Add("破绽联动 +8");}break;
                    case "huifeng":Shield(a,Round(10*mul));a.windUntil=a.ownTurn+1;break;
                    case "liuhuo":damage=16;if(a.windUntil>=a.ownTurn){a.windUntil=-1;b.burn=2;r.events.Add("蓄风联动：灼伤两次");}break;
                    case "huti":Shield(a,Round(16*mul));break;
                    case "qingxin":a.hp=Math.Min(100,a.hp+Round(10*mul));if(a.burn>0)a.burn=0;else a.flawUntil=-1;break;
                }
            }
            if(damage>0){if(valid&&a.counterUntil>=a.ownTurn){damage+=4;a.counterUntil=-1;r.events.Add("反照 +4");}Damage(b,Round(damage*(valid?mul:1)),true,r);}
            if(a.shieldUntil<=a.ownTurn){a.shield=0;a.shieldUntil=-1;}
            if(a.windUntil<=a.ownTurn)a.windUntil=-1;
            if(a.counterUntil<=a.ownTurn)a.counterUntil=-1;
            if(b.flawUntil<=a.ownTurn)b.flawUntil=-1;
            s.revision++;
            if(End(s))return r;
            if(s.turnId>=40){s.winner=a.hp==b.hp?-1:(s.fighters[0].hp>s.fighters[1].hp?0:1);r.events.Add("二十轮结束，按剩余生命裁定");return r;}
            s.turnId++;s.activeSeat=1-s.activeSeat;var next=s.fighters[s.activeSeat];next.ownTurn++;
            if(next.burn>0){next.burn--;Damage(next,3,false,r);r.events.Add("灼伤结算 3");}
            if(!End(s)&&next.ownTurn>1)next.energy=Math.Min(6,next.energy+1);
            return r;
        }
        private static int Round(double x)=>(int)Math.Round(x,MidpointRounding.AwayFromZero);
        private static void Shield(FighterState f,int value){f.shield=Math.Max(f.shield,value);f.shieldUntil=f.ownTurn+1;}
        private static void Damage(FighterState f,int damage,bool direct,BattleTransition r)
        {
            int absorbed=Math.Min(f.shield,damage);f.shield-=absorbed;f.hp=Math.Max(0,f.hp-damage+absorbed);
            if(direct&&absorbed>0&&f.mindset=="fanzhao")f.counterUntil=f.ownTurn+1;
            if(f.shieldUntil==int.MaxValue){f.shield=0;f.shieldUntil=-1;}
            r.events.Add("伤害 "+damage+" · 护盾吸收 "+absorbed);
        }
        private static bool End(BattleState s){if(s.fighters[0].hp<=0)s.winner=1;else if(s.fighters[1].hp<=0)s.winner=0;return s.winner!=-2;}
    }
}
