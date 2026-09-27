using NUnit.Framework;
using UnityEngine;
using Yibi.Battle;
using Yibi.Rules;

public class BattlePresentationTests
{
    [Test]
    public void SeatOneSeesItsOwnActionsWithoutRewritingUnrelatedEventText()
    {
        Assert.AreEqual("我方 · 裂石掌 · 100分", BattlePresentation.Perspective("对方 · 裂石掌 · 100分", 1));
        Assert.AreEqual("对方 · 点穴指", BattlePresentation.Perspective("我方 · 点穴指", 1));
        Assert.AreEqual("我方 · 点穴指", BattlePresentation.Perspective("我方 · 点穴指", 0));
        Assert.AreEqual("伤害 10 · 护盾吸收 5", BattlePresentation.Perspective("伤害 10 · 护盾吸收 5", 1));
    }

    [TestCase(0, "初成", "0.800")]
    [TestCase(69, "初成", "1.076")]
    [TestCase(70, "熟练", "1.080")]
    [TestCase(89, "熟练", "1.156")]
    [TestCase(90, "圆融", "1.160")]
    [TestCase(100, "圆融", "1.200")]
    public void GradeLabelsShowTheUnchangedContinuousMultiplier(int score, string tier, string multiplier)
    {
        var result = new ScoreResult { Valid = true, Score = score };
        StringAssert.Contains(tier, BattlePresentation.Grade(result));
        StringAssert.Contains("×" + multiplier, BattlePresentation.Grade(result));
        Assert.AreEqual(.8 + .4 * score / 100, result.Multiplier, 1e-10);
    }

    [Test]
    public void ProjectionUsesCorrectOwnerClockAndDoesNotChangeState()
    {
        var state = BattleReducer.Create("projection", 0);
        var fighter = state.fighters[0];
        fighter.ownTurn = 3; state.fighters[1].ownTurn = 7;
        fighter.flawUntil = 7; fighter.windUntil = 4; fighter.counterUntil = 2; fighter.burn = 1;
        fighter.eligible[0] = 4;
        string before = JsonUtility.ToJson(state);
        StringAssert.Contains("至对方第 7 次行动后", BattlePresentation.Effects(state, 0));
        StringAssert.Contains("至自己第 4 次行动后", BattlePresentation.Effects(state, 0));
        StringAssert.DoesNotContain("反照", BattlePresentation.Effects(state, 0));
        Assert.AreEqual(1, BattlePresentation.CooldownRemaining(fighter, "dianxue"));
        BattlePresentation.Combo(state, 0); BattlePresentation.SkillAvailability(state, 0, "dianxue");
        Assert.AreEqual(before, JsonUtility.ToJson(state));
    }

    [Test]
    public void InvalidFeedbackExplainsRecoveryAndTheFallbackCost()
    {
        foreach (var error in new[] { GestureError.TooShort, GestureError.WrongStart, GestureError.OutOfBounds })
        {
            string message = BattlePresentation.Recovery(ScoreResult.Invalid(error));
            StringAssert.DoesNotContain(error.ToString(), message);
            StringAssert.Contains("重新起笔", message);
            StringAssert.Contains("消耗回合但不耗内力", message);
        }
    }

    [Test]
    public void ShieldOnlyDamageStillUsesAttackPresentation()
    {
        var before = BattleReducer.Create("shield", 0); before.fighters[1].shield = 20;
        var after = before.Copy(); after.fighters[1].shield = 14;
        Assert.IsFalse(BattlePresentation.IsSupportAction(before, after, 0, "network"));
        Assert.IsTrue(BattlePresentation.IsSupportAction(before, before.Copy(), 0, "network"));
        Assert.IsFalse(BattlePresentation.IsSupportAction(before, after, 0, "basic"));
        Assert.IsTrue(BattlePresentation.IsSupportAction(before, after, 0, "rest"));
    }

    [Test]
    public void SettlementExplainsTheSameReducerResultFromEitherSeat()
    {
        var before = BattleReducer.Create("settlement", 0);
        var transition = BattleReducer.Apply(before, new BattleCommand { matchId = before.matchId, turnId = before.turnId, seat = 0, skillId = "basic" });
        string first = BattlePresentation.Settlement(before, transition.state, transition.events, 0);
        string second = BattlePresentation.Settlement(before, transition.state, transition.events, 1);
        StringAssert.Contains("我方 · 吐纳掌", first);
        StringAssert.Contains("对方 · 吐纳掌", second);
        StringAssert.Contains("我方 生命 100→99", second);
        StringAssert.Contains("护盾 5→0", second);
        Assert.AreEqual(100, before.fighters[1].hp);
    }

    [Test]
    public void DefaultNpcPrioritiesPreserveHealingWindAndEnergyChoices()
    {
        var state = BattleReducer.Create("npc", 0, null, new[] { "huifeng", "liuhuo", "qingxin" });
        var npc = state.fighters[1]; npc.energy = 4; npc.ownTurn = 2; npc.hp = 40; npc.windUntil = 4;
        string before = JsonUtility.ToJson(state);
        Assert.AreEqual("qingxin", NpcTacticProfile.SelectDefault(state, 1));
        Assert.AreEqual(before, JsonUtility.ToJson(state), "Preview must not change activeSeat or cooldowns.");
        npc.eligible[5] = 3;
        Assert.AreEqual("liuhuo", NpcTacticProfile.SelectDefault(state, 1));
        npc.windUntil = -1; npc.shield = 0;
        Assert.AreEqual("huifeng", NpcTacticProfile.SelectDefault(state, 1));
        npc.energy = 1;
        Assert.AreEqual("rest", NpcTacticProfile.SelectDefault(state, 1));
    }

    [Test]
    public void ConfiguredNpcRulesSkipUnusableEntriesAndFallBackSafely()
    {
        var state = BattleReducer.Create("custom", 0); state.fighters[1].energy = 2;
        var rules = new[] { new NpcTacticRule { skillId = "qingxin" }, new NpcTacticRule { skillId = "lieshi" }, new NpcTacticRule { skillId = "dianxue" } };
        Assert.AreEqual("dianxue", NpcTacticProfile.Select(state, 1, rules));
        state.fighters[1].eligible[0] = 8;
        Assert.AreEqual("basic", NpcTacticProfile.Select(state, 1, rules));
        Assert.AreEqual("basic", NpcTacticProfile.Select(state, 1, new[] { new NpcTacticRule { skillId = "unknown" } }));
    }

    [Test]
    public void PresentationConfigurationRejectsDuplicateUnknownIdsAndNonFiniteTiming()
    {
        var profile = ScriptableObject.CreateInstance<BattlePresentationProfile>();
        try
        {
            CollectionAssert.IsEmpty(profile.ValidateConfiguration());
            profile.techniques = new[] { new TechniquePresentation { skillId = "dianxue" }, new TechniquePresentation { skillId = "dianxue" }, new TechniquePresentation { skillId = "typo" } };
            profile.impactDelay = float.NaN; profile.actionDuration = -.1f;
            string errors = string.Join("\n", profile.ValidateConfiguration());
            StringAssert.Contains("重复技能 ID", errors); StringAssert.Contains("未知技能 ID", errors);
            StringAssert.Contains("行动时长", errors); StringAssert.Contains("命中延迟", errors);
        }
        finally { Object.DestroyImmediate(profile); }
    }

    [Test]
    public void TacticConfigurationRejectsMissingRulesUnknownConditionsAndImpossibleThresholds()
    {
        var profile = ScriptableObject.CreateInstance<NpcTacticProfile>();
        try
        {
            CollectionAssert.IsEmpty(profile.ValidateConfiguration());
            profile.priorities = new[] { null, new NpcTacticRule { skillId = "unknown", condition = (NpcTacticCondition)999 }, new NpcTacticRule { skillId = "rest", condition = NpcTacticCondition.EnergyAtMost, threshold = 7 } };
            string errors = string.Join("\n", profile.ValidateConfiguration());
            StringAssert.Contains("为空", errors); StringAssert.Contains("未知技能 ID", errors);
            StringAssert.Contains("未知条件", errors); StringAssert.Contains("内力阈值", errors);
            Assert.AreEqual("basic", profile.Select(BattleReducer.Create("validation", 0)), "Invalid conditions may not silently become Always.");
        }
        finally { Object.DestroyImmediate(profile); }
    }
}
