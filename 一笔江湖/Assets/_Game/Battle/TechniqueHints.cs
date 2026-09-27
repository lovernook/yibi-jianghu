namespace Yibi.Battle
{
    public static class TechniqueHints
    {
        public static string Describe(string id)
        {
            switch(id){
                case "dianxue":return "造成 8 基础伤害并留下破绽；接裂石掌可追加伤害。";
                case "lieshi":return "造成 18 基础伤害；消耗破绽追加 8 基础伤害。";
                case "huifeng":return "获得 10 基础护盾并蓄风；下次流火可附加灼伤。";
                case "liuhuo":return "造成 16 基础伤害；蓄风时附加两次各 3 点灼伤。";
                case "huti":return "获得 16 基础护盾，抵挡下一次进攻；反照心法可反击。";
                case "qingxin":return "回复 10 基础生命；优先清除灼伤，否则清除破绽。";
                default:return "调息回复 2 内力；吐纳掌无需画线，造成 6 点伤害。";
            }
        }
    }
}
