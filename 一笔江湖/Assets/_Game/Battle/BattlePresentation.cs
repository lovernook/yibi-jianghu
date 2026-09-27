using System;
using System.Collections.Generic;
using System.Globalization;
using Yibi.Rules;

namespace Yibi.Battle
{
    // A read-only projection. No clocks, Unity objects, networking, rewards or mutations.
    public static class BattlePresentation
    {
        public static string Perspective(string text, int localSeat)
        {
            if (string.IsNullOrEmpty(text) || localSeat != 1) return text;
            if (text.StartsWith("我方 · ", StringComparison.Ordinal)) return "对方 · " + text.Substring(5);
            if (text.StartsWith("对方 · ", StringComparison.Ordinal)) return "我方 · " + text.Substring(5);
            return text;
        }

        public static bool IsSupportAction(BattleState before, BattleState after, int seat, string id)
        {
            if (id != "network") return id == "rest" || id == "huifeng" || id == "huti" || id == "qingxin";
            if (before == null || after == null || seat < 0 || seat > 1) return true;
            // A shield-only impact is still an attack. This fallback is deliberately cosmetic.
            var oldTarget = before.fighters[1 - seat]; var target = after.fighters[1 - seat];
            return target.hp >= oldTarget.hp && target.shield >= oldTarget.shield;
        }

        public static int CooldownRemaining(FighterState fighter, string id)
        {
            int index = Array.IndexOf(BattleContent.Ids, id);
            return index < 0 || fighter == null ? 0 : Math.Max(0, fighter.eligible[index] - fighter.ownTurn);
        }

        public static string SkillAvailability(BattleState state, int seat, string id)
        {
            if (state == null || seat < 0 || seat > 1) return "等待对局";
            var fighter = state.fighters[seat];
            int index = Array.IndexOf(BattleContent.Ids, id);
            if (index < 0) return id == "rest" ? "恢复 2 内力" : "无需画线 · 不耗内力";
            int turns = CooldownRemaining(fighter, id);
            return "内力 " + BattleContent.Costs[index] + " / 现有 " + fighter.energy +
                   (turns > 0 ? " · 冷却 " + turns + " 个个人回合" : fighter.energy < BattleContent.Costs[index] ? " · 内力不足" : " · 可以运功");
        }

        public static string Grade(ScoreResult score)
        {
            if (score == null) return "待起笔 · 松开后评分";
            if (!score.Valid) return "运功失误 · 可重画";
            string tier = score.Score >= 90 ? "圆融" : score.Score >= 70 ? "熟练" : "初成";
            return tier + " · " + score.Score + " 分 · 效果 ×" + score.Multiplier.ToString("F3", CultureInfo.InvariantCulture);
        }

        public static string Breakdown(ScoreResult score)
        {
            if (score == null || !score.Valid) return "贴合 60% · 行进顺序 25% · 长度 15%";
            return "贴合 " + score.ShapeScore.ToString("F0") + " × 60%   顺序 " + score.OrderScore.ToString("F0") + " × 25%   长度 " + score.LengthScore.ToString("F0") + " × 15%";
        }

        public static string Recovery(ScoreResult score)
        {
            if (score == null) return "从 1 号穴位起笔；松开后可重画，确认才消耗行动。";
            if (score.Valid) return "确认后出招；也可重新起笔优化分数，回合计时不会重置。";
            string reason;
            switch (score.ErrorCode)
            {
                case GestureError.TooShort: reason = "笔迹太短，请按住左键连接至少两个不同位置。"; break;
                case GestureError.OutOfBounds: reason = "笔迹越出画板，请保持整笔在边框内。"; break;
                case GestureError.WrongStart: reason = "起点错误，请从 1 号穴位圆内起笔。"; break;
                case GestureError.WrongEnd: reason = "收笔位置错误，请在最后一个穴位圆内松开。"; break;
                case GestureError.MissingNode: reason = "漏过第 " + (score.FirstWrongNode + 1) + " 号穴位，请按编号逐个连通。"; break;
                case GestureError.WrongOrder: reason = "返回了第 " + (score.FirstWrongNode + 1) + " 号穴位，请沿编号前进。"; break;
                case GestureError.TooManyPoints: reason = "笔迹超过 512 点，请用更简洁的一笔重画。"; break;
                default: reason = "路线配置无效，请检查路线资产。"; break;
            }
            return reason + "\n重新起笔可重画；确认吐纳掌，消耗回合但不耗内力。";
        }

        public static string Effects(BattleState state, int seat)
        {
            if (state == null || seat < 0 || seat > 1) return "";
            var f = state.fighters[seat];
            var opponent = state.fighters[1 - seat];
            var lines = new List<string>(5);
            if (f.shield > 0) lines.Add(f.shieldUntil == int.MaxValue ? "后手补偿盾 " + f.shield + " · 首次受击后清除" : "护盾 " + f.shield + " · 至自己第 " + f.shieldUntil + " 次行动后");
            if (f.flawUntil >= opponent.ownTurn) lines.Add("破绽 · 至对方第 " + f.flawUntil + " 次行动后");
            if (f.windUntil >= f.ownTurn) lines.Add("蓄风 · 至自己第 " + f.windUntil + " 次行动后");
            if (f.counterUntil >= f.ownTurn) lines.Add("反照 +4 · 至自己第 " + f.counterUntil + " 次行动后");
            if (f.burn > 0) lines.Add("灼伤 " + f.burn + " 次 · 回合开始各 3 点");
            return lines.Count == 0 ? "无附加状态" : string.Join("\n", lines);
        }

        public static string Combo(BattleState state, int seat)
        {
            if (state == null || seat < 0 || seat > 1) return "选择三本秘籍，围绕破绽或蓄风构筑。";
            var f = state.fighters[seat]; var target = state.fighters[1 - seat];
            bool stone = Array.IndexOf(f.loadout, "lieshi") >= 0;
            bool fire = Array.IndexOf(f.loadout, "liuhuo") >= 0;
            if (stone && target.flawUntil >= f.ownTurn) return "破绽窗口 · 裂石掌可追加 8 基础伤害；留意内力与冷却。";
            if (fire && f.windUntil >= f.ownTurn) return "蓄风窗口 · 流火诀可追加两次灼伤。";
            if (stone && Array.IndexOf(f.loadout, "dianxue") >= 0) return "构筑思路 · 点穴 → 裂石；两招之间也要管理内力。";
            if (fire && Array.IndexOf(f.loadout, "huifeng") >= 0) return "构筑思路 · 回风 → 流火；防守后转入持续伤害。";
            if (f.mindset == "fanzhao") return "构筑思路 · 护盾承受直接攻击后，反照可强化下一击。";
            return "构筑思路 · 主动调息回复内力；守拙附带护盾。";
        }

        public static string Settlement(BattleState before, BattleState after, IList<string> events, int localSeat)
        {
            var lines = new List<string>();
            if (events != null) foreach (var line in events) lines.Add(Perspective(line, localSeat));
            if (before != null && after != null && before.matchId == after.matchId)
                for (int offset = 0; offset < 2; offset++)
                {
                    int seat = offset == 0 ? localSeat : 1 - localSeat;
                    if (seat < 0 || seat > 1) continue;
                    var a = before.fighters[seat]; var b = after.fighters[seat];
                    string label = offset == 0 ? "我方" : "对方";
                    lines.Add(label + " 生命 " + a.hp + "→" + b.hp + " · 护盾 " + a.shield + "→" + b.shield + " · 内力 " + a.energy + "→" + b.energy);
                }
            return lines.Count == 0 ? "尚未结算 · 选择行动后，结果将在这里保留。" : string.Join("\n", lines);
        }
    }
}
