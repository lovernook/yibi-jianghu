using UnityEngine;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.Battle
{
    // Every reference below is authored in a saved scene / Prefab. This view never builds UI or awards progress.
    [DisallowMultipleComponent]
    public sealed class BattleHUDView : MonoBehaviour
    {
        public BattlePresenter battle;
        public Text phaseLabel, intentLabel, comboLabel, selectionLabel, gradeLabel, breakdownLabel, recoveryLabel, settlementLabel, leftEffects, rightEffects;
        public Text[] skillHints;
        private int strokeVersion = -1;
        private bool wasDrawing, wasSample;
        private ScoreResult displayedScore;

        void OnEnable()
        {
            if (battle == null) return;
            battle.Changed += Refresh;
            battle.board.Changed += RefreshDrawingIfChanged;
            battle.board.Scored += Scored;
            Refresh();
        }
        void OnDisable()
        {
            if (battle == null) return;
            battle.Changed -= Refresh;
            if (battle.board != null) { battle.board.Changed -= RefreshDrawingIfChanged; battle.board.Scored -= Scored; }
        }
        void Scored(ScoreResult score) { RefreshDrawing(); }

        public void Refresh()
        {
            if (battle == null) return;
            var state = battle.State;
            if (state == null) return;
            int seat = battle.PlayerSeat;
            Put(phaseLabel, state.winner != -2 ? "论武已结算" : battle.IsResolving ? "招式结算 · 下一行动即将开始" : state.activeSeat == seat ? "你的行动 · 观察 → 选择 → 运笔 → 确认" : "对手行动 · 观察状态变化");
            Put(intentLabel, battle.IsOnline ? "真人对手 · 观察已用招式与状态，把握下一次行动。" : state.winner != -2 ? "本场结束；可调整构筑后再战。" : "当前局面倾向 · " + battle.TechniqueTitle(battle.PredictNpcSkill()) + "\n你行动后会重算，留意护盾、内力与状态。");
            Put(comboLabel, BattlePresentation.Combo(state, seat));
            Put(leftEffects, BattlePresentation.Effects(state, 0));
            Put(rightEffects, BattlePresentation.Effects(state, 1));
            Put(settlementLabel, string.IsNullOrEmpty(battle.LastResolution) ? "尚未结算 · 出招后保留最近行动及状态变化。" : battle.LastResolution);
            if (seat >= 0 && seat < 2 && skillHints != null)
                for (int i = 0; i < skillHints.Length && i < state.fighters[seat].loadout.Length; i++)
                    Put(skillHints[i], BattlePresentation.SkillAvailability(state, seat, state.fighters[seat].loadout[i]));
            RefreshDrawing();
        }

        void RefreshDrawingIfChanged()
        {
            var board = battle.board;
            // Point sampling can fire many times per frame. Text only changes at stroke/result boundaries.
            if (strokeVersion != board.StrokeVersion || wasDrawing != board.Sampler.IsDrawing || wasSample != board.IsProgramSample || displayedScore != board.LastResult)
                RefreshDrawing();
        }

        void RefreshDrawing()
        {
            var board = battle.board;
            strokeVersion = board.StrokeVersion; wasDrawing = board.Sampler.IsDrawing; wasSample = board.IsProgramSample; displayedScore = board.LastResult;
            string id = battle.SelectedSkill;
            Put(selectionLabel, string.IsNullOrEmpty(id) ? "选择一门秘籍，查看本招与连招用途。" : battle.TechniqueTitle(id) + " · " + battle.TechniqueDescription(id) + "\n" + battle.TechniqueHint(id));
            Put(gradeLabel, wasSample ? "练功程序样本 · 不可用于出招" : wasDrawing ? "运笔中 · 松开后评分" : BattlePresentation.Grade(displayedScore));
            Put(breakdownLabel, BattlePresentation.Breakdown(displayedScore));
            Put(recoveryLabel, wasSample ? "请重新用鼠标起笔；示范与固定样本不会替你提交战斗行动。" : BattlePresentation.Recovery(displayedScore));
            if(battle.confirmButton!=null)Put(battle.confirmButton.GetComponentInChildren<Text>(),displayedScore!=null&&!displayedScore.Valid&&!wasSample?"确认 · 吐纳掌":"确认施放");
        }

        static void Put(Text target, string value) { if (target != null && target.text != value) target.text = value; }
    }
}
