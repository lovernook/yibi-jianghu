using System;
using System.Collections.Generic;
using Yibi.Rules;

namespace Yibi.Progression
{
    public sealed class QuestRewardGrant
    {
        public string QuestId;
        public string LegacyAchievementId;
        public QuestReward Reward;
    }

    public sealed class QuestChange
    {
        public QuestProgressState State { get; internal set; }
        public IReadOnlyList<QuestRewardGrant> Grants { get; internal set; }
        public bool Changed { get; internal set; }
    }

    // The service does not know PlayerProfile, JSON, MonoBehaviours, or the scene that reported a fact.
    public interface IQuestProgressRepository
    {
        long Revision { get; }
        QuestProgressState Read();
        bool TryCommit(QuestProgressState next, IReadOnlyList<QuestRewardGrant> grants);
    }

    public sealed class QuestService
    {
        private readonly QuestDefinition[] definitions;
        private readonly LegacyFactMapping[] legacyFacts;
        private readonly Dictionary<string, QuestDefinition> byId = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);
        private readonly IQuestProgressRepository repository;
        private IReadOnlyList<QuestSnapshot> cachedSnapshots;
        private long snapshotRevision = -1;

        public QuestService(QuestCatalogSO catalog, IQuestProgressRepository repository)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (repository == null) throw new ArgumentNullException("repository");
            this.repository = repository;
            definitions = catalog.BuildDefinitions();
            legacyFacts = catalog.BuildLegacyFactMappings();
            foreach (var definition in definitions) byId.Add(definition.Id, definition);
        }

        public IReadOnlyList<QuestSnapshot> GetSnapshots()
        {
            if (cachedSnapshots != null && snapshotRevision == repository.Revision) return cachedSnapshots;
            var state = repository.Read();
            // If a migration write failed, legacy receipts are still authoritative for display.
            // This projection changes only the isolated read state and performs no persistence.
            ImportLegacyReceipts(state);
            var snapshots = new QuestSnapshot[definitions.Length];
            for (int i = 0; i < definitions.Length; i++) snapshots[i] = QuestRules.Snapshot(definitions[i], state);
            cachedSnapshots = Array.AsReadOnly(snapshots);
            snapshotRevision = repository.Revision;
            return cachedSnapshots;
        }

        public QuestSnapshot GetTracked()
        {
            QuestSnapshot locked = null;
            foreach (var snapshot in GetSnapshots())
            {
                if (!snapshot.IsMain || snapshot.State == QuestState.Claimed) continue;
                if (snapshot.State != QuestState.Locked) return snapshot;
                if (locked == null) locked = snapshot;
            }
            return locked;
        }

        public bool IsClaimed(string id)
        {
            var state = repository.Read(); ImportLegacyReceipts(state);
            return state.ClaimedQuestIds.Contains(id);
        }
        public bool CanClaim(string id)
        {
            QuestDefinition definition;
            if (id == null || !byId.TryGetValue(id, out definition)) return false;
            var state = repository.Read(); ImportLegacyReceipts(state);
            return QuestRules.State(definition, state) == QuestState.Ready;
        }

        // A receipt migration must never infer a new reward from an old score or from opening a screen.
        public bool MigrateLegacy()
        {
            var state = repository.Read();
            bool changed = ImportLegacyReceipts(state);
            return changed && repository.TryCommit(state, new QuestRewardGrant[0]);
        }

        public bool RecordFact(string key, int value)
        {
            var change = PrepareFacts(repository.Read(), new[] { new QuestFactRecord { key = key, value = value } });
            return change.Changed && repository.TryCommit(change.State, change.Grants);
        }

        // Pure transaction preparation lets a repository atomically save a practice record together
        // with its derived facts and rewards. This method does not save or publish anything.
        public QuestChange PrepareFacts(QuestProgressState source, IReadOnlyList<QuestFactRecord> facts)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (facts == null) throw new ArgumentNullException("facts");
            var state = source.Copy();
            bool changed = ImportLegacyReceipts(state);
            foreach (var fact in facts)
            {
                if (fact == null) throw new ArgumentException("A fact cannot be null.", "facts");
                ValidateFact(fact.key, fact.value);
                changed |= state.SetMaximum(fact.key, fact.value);
            }
            var grants = new List<QuestRewardGrant>();
            changed |= CollectAutomatic(state, grants);
            return new QuestChange { State = state, Grants = grants.AsReadOnly(), Changed = changed };
        }

        public bool AddFact(string key, int amount)
        {
            ValidateFact(key, amount);
            int value = checked(repository.Read().Fact(key) + amount);
            return RecordFact(key, value);
        }

        public bool ClaimQuest(string id)
        {
            QuestDefinition definition;
            if (id == null || !byId.TryGetValue(id, out definition)) return false;
            var state = repository.Read();
            ImportLegacyReceipts(state);
            if (QuestRules.State(definition, state) != QuestState.Ready) return false;
            var grants = new List<QuestRewardGrant>();
            Claim(definition, state, grants);
            CollectAutomatic(state, grants);
            return repository.TryCommit(state, grants);
        }

        // Compatibility entry for old saves and reusable world caches. The requested reward wins;
        // a matching quest's configured reward is not also paid.
        public bool GrantLegacy(string achievement, int tickets, string incrementFact = null)
        {
            if (string.IsNullOrWhiteSpace(achievement)) throw new ArgumentException("A reward requires a stable achievement ID.", "achievement");
            if (tickets < 0) throw new ArgumentOutOfRangeException("tickets");
            var state = repository.Read();
            if (state.LegacyAchievements.Contains(achievement)) return false;
            state.LegacyAchievements.Add(achievement);
            if (incrementFact != null)
            {
                ValidateFact(incrementFact, 0);
                state.SetMaximum(incrementFact, checked(state.Fact(incrementFact) + 1));
            }
            ImportLegacyReceipts(state);
            var grants = new List<QuestRewardGrant> {
                new QuestRewardGrant { LegacyAchievementId = achievement,
                    Reward = new QuestReward { Id = achievement, Tickets = tickets } }
            };
            CollectAutomatic(state, grants);
            return repository.TryCommit(state, grants);
        }

        private bool ImportLegacyReceipts(QuestProgressState state)
        {
            bool changed = false;
            foreach (var definition in definitions)
            {
                if (string.IsNullOrEmpty(definition.LegacyAchievementId) || !state.LegacyAchievements.Contains(definition.LegacyAchievementId)) continue;
                if (!state.ClaimedQuestIds.Contains(definition.Id)) { state.ClaimedQuestIds.Add(definition.Id); changed = true; }
            }
            foreach (var mapping in legacyFacts)
            {
                int count = 0;
                foreach (var achievement in mapping.achievementIds)
                    if (state.LegacyAchievements.Contains(achievement)) count++;
                // The explicit set avoids treating unrelated similarly named achievements as
                // collection progress. Maximum semantics preserve newer/larger saved values.
                changed |= state.SetMaximum(mapping.factKey, count);
            }
            return changed;
        }

        private bool CollectAutomatic(QuestProgressState state, List<QuestRewardGrant> grants)
        {
            bool any = false, changed;
            // Validated dependency graphs are acyclic. Each successful pass claims at least one
            // previously unclaimed definition, so this loop is bounded by the catalog size.
            do
            {
                changed = false;
                foreach (var definition in definitions)
                {
                    if (!definition.AutoClaim || QuestRules.State(definition, state) != QuestState.Ready) continue;
                    Claim(definition, state, grants); changed = any = true;
                }
            } while (changed);
            return any;
        }

        private static void Claim(QuestDefinition definition, QuestProgressState state, List<QuestRewardGrant> grants)
        {
            state.ClaimedQuestIds.Add(definition.Id);
            bool alreadyPaid = !string.IsNullOrEmpty(definition.LegacyAchievementId) && state.LegacyAchievements.Contains(definition.LegacyAchievementId);
            if (alreadyPaid) return;
            if (!string.IsNullOrEmpty(definition.LegacyAchievementId)) state.LegacyAchievements.Add(definition.LegacyAchievementId);
            grants.Add(new QuestRewardGrant { QuestId = definition.Id, LegacyAchievementId = definition.LegacyAchievementId, Reward = definition.Reward });
        }

        private static void ValidateFact(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A fact requires a stable key.", "key");
            if (value < 0) throw new ArgumentOutOfRangeException("value");
        }
    }
}
