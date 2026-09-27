using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yibi.Battle;
using Yibi.Progression;
using Yibi.Rules;

public class QuestProgressionTests
{
    private readonly List<ScriptableObject> created = new List<ScriptableObject>();

    [TearDown]
    public void ReleaseTestDefinitions()
    {
        foreach (var asset in created) UnityEngine.Object.DestroyImmediate(asset);
        created.Clear();
    }

    [Test]
    public void DuplicateStableIdsAreRejectedBeforeRuntimeUsesTheCatalog()
    {
        var first = Quest("duplicate", "fact.a", 1);
        var second = Quest("duplicate", "fact.b", 1);
        Assert.IsNotEmpty(Catalog(first, second).Validate());
    }

    [Test]
    public void CyclicPrerequisitesAreRejectedButAnAcyclicChainIsValid()
    {
        var first = Quest("one", "fact.a", 1);
        var second = Quest("two", "fact.b", 1);
        second.prerequisites = new[] { first };
        var catalog = Catalog(first, second);
        CollectionAssert.IsEmpty(catalog.Validate());
        first.prerequisites = new[] { second };
        Assert.IsNotEmpty(catalog.Validate());
    }

    [Test]
    public void RemovingARequiredDefinitionIsDetectedBeforeAnUnreachableQuestShips()
    {
        var first = Quest("removed", "fact.a", 1);
        var second = Quest("dependent", "fact.b", 1);
        second.prerequisites = new[] { first };
        Assert.IsNotEmpty(Catalog(second).Validate());
    }

    [Test]
    public void CompletionOnlyChapterAcceptsPrerequisitesWithoutAnArtificialCounter()
    {
        var first = Quest("practice", "fact.practice", 1);
        var chapter = Quest("chapter", "unused", 1);
        chapter.objectives = Array.Empty<QuestObjective>();
        chapter.prerequisites = new[] { first };
        CollectionAssert.IsEmpty(Catalog(first, chapter).Validate());
    }

    [Test]
    public void NewFactsAndDifferentThresholdsNeedOnlyCatalogChanges()
    {
        var low = Quest("herbs", "gather.herb", 3, 2);
        var high = Quest("rare-herbs", "gather.herb", 5, 4);
        var unrelated = Quest("boat", "travel.boat", 1, 1);
        var profile = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(profile, Catalog(low, high, unrelated)))
        {
            var service = ProfileStore.Progress;
            Assert.IsTrue(service.AddFact("gather.herb", 2));
            Assert.IsFalse(service.CanClaim(low.stableId));
            Assert.IsTrue(service.AddFact("gather.herb", 1));
            Assert.IsTrue(service.CanClaim(low.stableId));
            Assert.IsFalse(service.CanClaim(high.stableId));
            Assert.IsFalse(service.CanClaim(unrelated.stableId));
            Assert.IsTrue(service.ClaimQuest(low.stableId));
            Assert.AreEqual(4, profile.tickets);
            Assert.IsTrue(service.RecordFact("gather.herb", 5));
            Assert.IsTrue(service.CanClaim(high.stableId));
            Assert.IsFalse(service.RecordFact("gather.herb", 2), "Best/count facts never decrease.");
            Assert.AreEqual(5, profile.questFacts.Single(x => x.key == "gather.herb").value);
        }
    }

    [Test]
    public void ManualClaimIsRequiredAndCannotPayTwice()
    {
        var quest = Quest("manual", "explore.shrine", 1, 3);
        quest.reward.fragments = 2;
        quest.reward.unlockTechniqueIds = new[] { "huti" };
        var profile = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(profile, Catalog(quest)))
        {
            var service = ProfileStore.Progress;
            Assert.IsFalse(service.ClaimQuest(quest.stableId));
            Assert.IsTrue(service.RecordFact("explore.shrine", 1));
            Assert.AreEqual(2, profile.tickets, "Completing a manual task only makes it ready.");
            Assert.AreEqual(QuestState.Ready, service.GetSnapshots().Single().State);
            Assert.IsTrue(service.ClaimQuest(quest.stableId));
            Assert.IsFalse(service.ClaimQuest(quest.stableId));
            Assert.IsFalse(service.ClaimQuest("unknown.quest"));
            Assert.AreEqual(5, profile.tickets);
            Assert.AreEqual(2, profile.fragments);
            Assert.AreEqual(1, profile.unlocked.Count(x => x == "huti"));
            Assert.AreEqual(1, profile.claimedQuests.Count(x => x == quest.stableId));
        }
    }

    [Test]
    public void ProgressRecordedBeforePrerequisiteIsKeptAndAutoChainSettlesOnce()
    {
        var first = Quest("first", "fact.first", 1, 1);
        var second = Quest("second", "fact.second", 2, 2, true);
        second.prerequisites = new[] { first };
        var chapter = Quest("end", "unused", 1, 3, true);
        chapter.objectives = Array.Empty<QuestObjective>();
        chapter.prerequisites = new[] { second };
        var profile = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(profile, Catalog(chapter, second, first)))
        {
            var service = ProfileStore.Progress;
            Assert.IsTrue(service.RecordFact("fact.second", 2));
            Assert.AreEqual(QuestState.Locked, service.GetSnapshots().Single(x => x.Id == second.stableId).State);
            Assert.AreEqual(2, profile.tickets);
            service.RecordFact("fact.first", 1);
            Assert.IsTrue(service.ClaimQuest(first.stableId));
            Assert.IsTrue(service.IsClaimed(second.stableId));
            Assert.IsTrue(service.IsClaimed(chapter.stableId));
            Assert.AreEqual(8, profile.tickets);
            Assert.IsFalse(service.RecordFact("fact.second", 2));
            Assert.AreEqual(8, profile.tickets);
        }
    }

    [Test]
    public void LegacyReceiptsMigrateWithoutReissuingCurrencyOrNewUnlockRewards()
    {
        var quest = Quest("legacy", "score.best", 70, 5, true);
        quest.legacyAchievementId = "old.practice.reward";
        quest.reward.unlockTechniqueIds = new[] { "huti" };
        var profile = new PlayerProfile { schemaVersion = 1, tickets = 17, fragments = 4 };
        profile.achievements.Add("old.practice.reward");
        profile.achievements.Add("future.achievement");
        profile.unlocked.Add("qingxin");
        profile.questFacts = new List<QuestFactRecord> { new QuestFactRecord { key = "future.counter", value = 9 } };
        profile.claimedQuests = new List<string> { "future.quest" };
        using (ProfileStore.UseTransientProfile(profile, Catalog(quest)))
        {
            var service = ProfileStore.Progress;
            Assert.AreEqual(1, profile.schemaVersion, "The additive migration retains compatibility with schema-1 saves.");
            Assert.IsTrue(service.IsClaimed(quest.stableId));
            Assert.AreEqual(17, profile.tickets);
            Assert.AreEqual(4, profile.fragments);
            CollectionAssert.Contains(profile.unlocked, "qingxin");
            CollectionAssert.DoesNotContain(profile.unlocked, "huti", "Migration records earlier payment; it is not a fresh grant.");
            CollectionAssert.Contains(profile.achievements, "future.achievement");
            CollectionAssert.Contains(profile.claimedQuests, "future.quest");
            Assert.AreEqual(9, profile.questFacts.Single(x => x.key == "future.counter").value);
            Assert.IsFalse(service.ClaimQuest(quest.stableId));
            Assert.IsFalse(service.MigrateLegacy());
            Assert.AreEqual(17, profile.tickets);
        }
    }

    [Test]
    public void RemovingAndReaddingAnAssetKeepsItsReceiptAndDoesNotRepayIt()
    {
        var quest = Quest("optional", "visit.west", 1, 2, true);
        var full = Catalog(quest);
        var empty = Catalog();
        var profile = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(profile, full))
            Assert.IsTrue(ProfileStore.Progress.RecordFact("visit.west", 1));
        Assert.AreEqual(4, profile.tickets);
        using (ProfileStore.UseTransientProfile(profile, empty))
        {
            CollectionAssert.IsEmpty(ProfileStore.Progress.GetSnapshots());
            CollectionAssert.Contains(profile.claimedQuests, quest.stableId);
            Assert.AreEqual(1, profile.questFacts.Single(x => x.key == "visit.west").value);
        }
        using (ProfileStore.UseTransientProfile(profile, full))
        {
            Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
            Assert.IsFalse(ProfileStore.Progress.RecordFact("visit.west", 1));
            Assert.IsFalse(ProfileStore.Progress.ClaimQuest(quest.stableId));
            Assert.AreEqual(4, profile.tickets);
        }
    }

    [Test]
    public void ReadingSnapshotsNeverCommitsOrClaimsReadyRewards()
    {
        var quest = Quest("read", "readiness", 1, 4, true);
        var profile = new PlayerProfile();
        profile.questFacts.Add(new QuestFactRecord { key = "readiness", value = 1 });
        using (ProfileStore.UseTransientProfile(profile, Catalog(quest)))
        {
            var service = ProfileStore.Progress;
            string before = JsonUtility.ToJson(profile);
            int writes = 0, changes = 0;
            Action changed = () => changes++;
            ProfileStore.Changed += changed;
            try
            {
                using (ProfileStore.UseSaveOverride(_ => writes++))
                {
                    var first = service.GetSnapshots();
                    for (int i = 0; i < 20; i++)
                    {
                        Assert.AreSame(first, service.GetSnapshots(), "Unchanged progress reuses its immutable view snapshot.");
                        Assert.IsNotNull(service.GetTracked());
                        Assert.IsTrue(service.CanClaim(quest.stableId));
                        Assert.IsFalse(service.IsClaimed(quest.stableId));
                    }
                }
            }
            finally { ProfileStore.Changed -= changed; }
            Assert.AreEqual(0, writes);
            Assert.AreEqual(0, changes);
            Assert.AreEqual(before, JsonUtility.ToJson(profile));
        }
    }

    [Test]
    public void FailedSaveRollsBackTheWholeFactAndRewardTransactionAndCanBeRetried()
    {
        var quest = Quest("atomic", "atomic.event", 1, 3, true);
        quest.reward.unlockTechniqueIds = new[] { "huti" };
        var profile = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(profile, Catalog(quest)))
        {
            var service = ProfileStore.Progress;
            string before = JsonUtility.ToJson(profile);
            int changes = 0;
            Action changed = () => changes++;
            ProfileStore.Changed += changed;
            try
            {
                using (ProfileStore.UseSaveOverride(_ => { throw new IOException("Simulated storage full"); }))
                    Assert.IsFalse(service.RecordFact("atomic.event", 1));
                Assert.AreSame(profile, ProfileStore.Current);
                Assert.AreEqual(before, JsonUtility.ToJson(profile));
                Assert.AreEqual(0, changes);
                Assert.IsFalse(service.IsClaimed(quest.stableId));
                Assert.IsTrue(service.RecordFact("atomic.event", 1));
                Assert.AreEqual(5, profile.tickets);
                CollectionAssert.Contains(profile.unlocked, "huti");
                Assert.AreEqual(1, changes);
            }
            finally { ProfileStore.Changed -= changed; }
        }
    }

    [Test]
    public void FailedManualClaimStaysReadyAndSuccessfulRetryPaysOnlyOnce()
    {
        var quest = Quest("claim-retry", "claim.ready", 1, 3);
        var profile = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(profile, Catalog(quest)))
        {
            var service = ProfileStore.Progress;
            service.RecordFact("claim.ready", 1);
            string before = JsonUtility.ToJson(profile);
            using (ProfileStore.UseSaveOverride(_ => { throw new IOException("Simulated failed replacement"); }))
                Assert.IsFalse(service.ClaimQuest(quest.stableId));
            Assert.AreEqual(before, JsonUtility.ToJson(profile));
            Assert.IsTrue(service.CanClaim(quest.stableId));
            StringAssert.StartsWith("保存失败", ProfileStore.Notice);
            Assert.IsTrue(service.ClaimQuest(quest.stableId));
            Assert.IsFalse(service.ClaimQuest(quest.stableId));
            Assert.AreEqual(5, profile.tickets);
        }
    }

    [Test]
    public void NestedTransientProfilesRestoreTheOriginalServiceAndDoNotCrossAward()
    {
        var quest = Quest("identity", "actor.event", 1, 2, true);
        var catalog = Catalog(quest);
        var alice = new PlayerProfile();
        var bob = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(alice, catalog))
        {
            var aliceService = ProfileStore.Progress;
            Assert.IsTrue(aliceService.RecordFact("actor.event", 1));
            using (ProfileStore.UseTransientProfile(bob, catalog))
            {
                Assert.AreNotSame(aliceService, ProfileStore.Progress);
                Assert.Throws<InvalidOperationException>(() => aliceService.GetSnapshots(),
                    "A delayed view callback cannot read the next player's profile through the old service.");
                Assert.Throws<InvalidOperationException>(() => aliceService.RecordFact("actor.event", 99),
                    "A stale gameplay callback must not write into the next player's profile.");
                Assert.IsFalse(ProfileStore.Progress.IsClaimed(quest.stableId));
                Assert.IsTrue(ProfileStore.Progress.RecordFact("actor.event", 1));
                Assert.AreEqual(4, bob.tickets);
            }
            Assert.AreSame(alice, ProfileStore.Current);
            Assert.AreSame(aliceService, ProfileStore.Progress);
            Assert.IsTrue(ProfileStore.Progress.IsClaimed(quest.stableId));
            Assert.IsFalse(ProfileStore.Progress.RecordFact("actor.event", 1));
            Assert.AreEqual(4, alice.tickets);
            Assert.AreEqual(4, bob.tickets);
        }
    }

    [Test]
    public void JsonRoundTripPreservesManualReadyStateUntilOneExplicitClaim()
    {
        var quest = Quest("saved", "saved.fact", 2, 1);
        var catalog = Catalog(quest);
        var original = new PlayerProfile();
        using (ProfileStore.UseTransientProfile(original, catalog))
            ProfileStore.Progress.RecordFact("saved.fact", 2);
        var restored = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(original));
        using (ProfileStore.UseTransientProfile(restored, catalog))
        {
            Assert.IsTrue(ProfileStore.Progress.CanClaim(quest.stableId));
            Assert.AreEqual(2, restored.tickets);
            Assert.IsTrue(ProfileStore.Progress.ClaimQuest(quest.stableId));
            Assert.IsFalse(ProfileStore.Progress.ClaimQuest(quest.stableId));
            Assert.AreEqual(3, restored.tickets);
        }
    }

    [Test]
    public void ExplicitLegacyCacheMappingRestoresReadyProgressWithoutReopeningOrPayingOldCaches()
    {
        var quest = Quest("cache-survey", "cache.count", 3, 0);
        quest.reward.fragments = 2;
        var automatic = Quest("new-auto-cache-task", "cache.count", 3, 4, true);
        var catalog = Catalog(quest, automatic);
        catalog.legacyFactMappings = new[] {
            new LegacyFactMapping { factKey = "cache.count", achievementIds = new[] {
                "cache-guiyun-1", "cache-guiyun-2", "cache-guiyun-3" } }
        };
        var profile = new PlayerProfile { tickets = 12, fragments = 5 };
        profile.achievements.AddRange(new[] {
            "cache-guiyun-1", "cache-guiyun-2", "cache-guiyun-3", "cache-unrelated-area" });
        using (ProfileStore.UseTransientProfile(profile, catalog))
        {
            var service = ProfileStore.Progress;
            Assert.AreEqual(3, profile.questFacts.Single(x => x.key == "cache.count").value);
            Assert.IsTrue(service.CanClaim(quest.stableId));
            Assert.IsFalse(service.IsClaimed(automatic.stableId), "Migration never automatically grants a newly introduced reward.");
            Assert.AreEqual(12, profile.tickets);
            Assert.AreEqual(5, profile.fragments);
            Assert.IsFalse(service.MigrateLegacy(), "An unchanged explicit mapping is idempotent.");
            Assert.IsFalse(ProfileStore.Reward("cache-guiyun-1", 1, "cache.count"));
            Assert.AreEqual(3, profile.questFacts.Single(x => x.key == "cache.count").value);
            Assert.IsTrue(service.ClaimQuest(quest.stableId));
            Assert.IsFalse(service.ClaimQuest(quest.stableId));
            Assert.AreEqual(7, profile.fragments);
            Assert.AreEqual(16, profile.tickets, "A later explicit claim can settle newly ready automatic tasks once.");
            CollectionAssert.Contains(profile.achievements, "cache-unrelated-area");
        }
    }

    [Test]
    public void LegacyFactMappingsRejectDuplicateIdsAndKeepHigherExistingProgress()
    {
        var catalog = Catalog(Quest("mapped", "cache.count", 3, 0));
        catalog.legacyFactMappings = new[] {
            new LegacyFactMapping { factKey = "cache.count", achievementIds = new[] { "cache-a", "cache-a" } }
        };
        Assert.IsNotEmpty(catalog.Validate());
        catalog.legacyFactMappings[0].achievementIds = new[] { "cache-a", "cache-b" };
        CollectionAssert.IsEmpty(catalog.Validate());
        var profile = new PlayerProfile();
        profile.achievements.Add("cache-a");
        profile.questFacts.Add(new QuestFactRecord { key = "cache.count", value = 8 });
        using (ProfileStore.UseTransientProfile(profile, catalog))
        {
            Assert.IsFalse(ProfileStore.Progress.MigrateLegacy());
            Assert.AreEqual(8, profile.questFacts.Single(x => x.key == "cache.count").value);
        }
    }

    private T Asset<T>() where T : ScriptableObject
    {
        var value = ScriptableObject.CreateInstance<T>();
        created.Add(value);
        return value;
    }

    private QuestDefinitionSO Quest(string id, string fact, int threshold, int tickets = 1, bool automatic = false)
    {
        var reward = Asset<RewardDefinitionSO>();
        reward.stableId = "test.reward." + id;
        reward.tickets = tickets;
        reward.unlockTechniqueIds = Array.Empty<string>();
        var quest = Asset<QuestDefinitionSO>();
        quest.stableId = "test." + id;
        quest.title = "Test " + id;
        quest.description = "Created only in memory for behavior verification.";
        quest.autoClaim = automatic;
        quest.prerequisites = Array.Empty<QuestDefinitionSO>();
        quest.objectives = new[] { new QuestObjective { factKey = fact, requiredValue = threshold, label = fact } };
        quest.reward = reward;
        return quest;
    }

    private QuestCatalogSO Catalog(params QuestDefinitionSO[] quests)
    {
        var catalog = Asset<QuestCatalogSO>();
        catalog.quests = quests;
        return catalog;
    }
}
