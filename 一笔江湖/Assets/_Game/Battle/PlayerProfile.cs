using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Yibi.Rules;
using Yibi.Progression;

namespace Yibi.Battle
{
    [Serializable] public sealed class PracticeRecord
    {
        public string routeId;
        public int attempts, bestScore, lastScore;
    }

    [Serializable] public sealed class PlayerProfile
    {
        public int schemaVersion = 1, tickets = 2, fragments;
        public string contentVersion = "demo-1", mindset = "shouzhuo";
        public string[] loadout = { "dianxue", "lieshi", "huifeng" };
        public List<string> unlocked = new List<string> { "dianxue", "lieshi", "huifeng" };
        public List<string> achievements = new List<string>();
        public List<PracticeRecord> practice = new List<PracticeRecord>();
        // Additive optional fields keep original schema-1 saves readable. Never delete unknown
        // receipts or facts: removed content can be reintroduced without paying a reward twice.
        public List<QuestFactRecord> questFacts = new List<QuestFactRecord>();
        public List<string> claimedQuests = new List<string>();
    }

    public static class ProfileStore
    {
        private const string CatalogResourcePath = "Progression/QuestCatalog";
        public static string PathName { get { return Path.Combine(Application.persistentDataPath, "Profile.json"); } }
        public static PlayerProfile Current { get; private set; }
        public static string Notice { get; private set; }
        public static event Action Changed;
        public static int NpcStrategy;
        public static Vector3 ReturnPosition = new Vector3(0, 1, -14);
        public static bool HasReturnPosition;
        private static bool transient;
        private static object profileToken = new object();
        private static long revision;
        private static QuestService progress;
        private static QuestCatalogSO catalogOverride;
        private static Action<PlayerProfile> saveOverride;

        public static QuestService Progress
        {
            get
            {
                Load();
                if (progress != null) return progress;
                var catalog = catalogOverride != null ? catalogOverride : Resources.Load<QuestCatalogSO>(CatalogResourcePath);
                if (catalog == null) throw new InvalidOperationException("任务目录未配置：Resources/" + CatalogResourcePath + "。请先完成阶段 B 场景与数据安装。");
                progress = new QuestService(catalog, new ProfileQuestRepository(profileToken));
                progress.MigrateLegacy();
                return progress;
            }
        }

        public static IDisposable UseTransientProfile(PlayerProfile profile, QuestCatalogSO catalog = null)
        {
            Validate(profile);
            var scope = new TransientScope();
            Current = profile; Notice = ""; HasReturnPosition = false; NpcStrategy = 0;
            transient = true; progress = null; catalogOverride = catalog;
            profileToken = new object(); revision++;
            return scope;
        }

        // An isolated persistence adapter makes failure paths testable without writing the user's save.
        public static IDisposable UseSaveOverride(Action<PlayerProfile> writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            var scope = new SaveScope(saveOverride); saveOverride = writer; return scope;
        }

        private sealed class SaveScope : IDisposable
        {
            private readonly Action<PlayerProfile> previous;
            private bool disposed;
            public SaveScope(Action<PlayerProfile> previous) { this.previous = previous; }
            public void Dispose() { if (disposed) return; disposed = true; saveOverride = previous; }
        }

        private sealed class TransientScope : IDisposable
        {
            private readonly PlayerProfile saved = Current;
            private readonly string notice = Notice;
            private readonly bool previousTransient = transient, hasReturn = HasReturnPosition;
            private readonly int strategy = NpcStrategy;
            private readonly Vector3 position = ReturnPosition;
            private readonly QuestService savedProgress = progress;
            private readonly QuestCatalogSO savedCatalog = catalogOverride;
            private readonly object savedToken = profileToken;
            private readonly long savedRevision = revision;
            private readonly Action<PlayerProfile> savedWriter = saveOverride;
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                Current = saved; Notice = notice; transient = previousTransient; HasReturnPosition = hasReturn;
                NpcStrategy = strategy; ReturnPosition = position; progress = savedProgress;
                catalogOverride = savedCatalog; profileToken = savedToken; revision = savedRevision;
                saveOverride = savedWriter;
            }
        }

        public static void Load()
        {
            if (Current != null) return;
            Notice = "";
            foreach (var path in new[] { PathName, PathName + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                    Validate(profile); Current = profile;
                    if (path.EndsWith(".bak", StringComparison.Ordinal)) Notice = "存档损坏，已恢复备份。";
                    return;
                }
                catch (Exception ex) { Notice = "存档读取失败：" + ex.GetType().Name + "；已尝试备份。"; }
            }
            Current = new PlayerProfile();
            if (Notice != "") Notice += "使用新档，损坏文件保留。";
        }

        public static void Validate(PlayerProfile profile)
        {
            if (profile == null || profile.schemaVersion != 1 || profile.loadout == null || profile.loadout.Length != 3 ||
                profile.unlocked == null || profile.achievements == null || profile.tickets < 0 || profile.fragments < 0)
                throw new InvalidDataException("Invalid profile");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in profile.loadout)
                if (Array.IndexOf(BattleContent.Ids, id) < 0 || !profile.unlocked.Contains(id) || !ids.Add(id))
                    throw new InvalidDataException("Invalid loadout");
            if (profile.mindset != "shouzhuo" && profile.mindset != "fanzhao") throw new InvalidDataException("Invalid mindset");
            if (profile.practice == null) profile.practice = new List<PracticeRecord>();
            var routes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in profile.practice)
                if (record == null || Array.IndexOf(BattleContent.Ids, record.routeId) < 0 || !routes.Add(record.routeId) || record.attempts < 0 ||
                    record.bestScore < 0 || record.bestScore > 100 || record.lastScore < 0 || record.lastScore > 100)
                    throw new InvalidDataException("Invalid practice record");
            if (profile.questFacts == null) profile.questFacts = new List<QuestFactRecord>();
            if (profile.claimedQuests == null) profile.claimedQuests = new List<string>();
            var facts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fact in profile.questFacts)
                if (fact == null || string.IsNullOrWhiteSpace(fact.key) || fact.value < 0 || !facts.Add(fact.key))
                    throw new InvalidDataException("Invalid quest fact");
            var receipts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in profile.claimedQuests)
                if (string.IsNullOrWhiteSpace(id) || !receipts.Add(id)) throw new InvalidDataException("Invalid quest receipt");
        }

        public static void Save()
        {
            Load(); Validate(Current); Persist(Clone(Current)); revision++; PublishChanged();
        }

        private static void Persist(PlayerProfile profile)
        {
            Validate(profile);
            if (saveOverride != null) { saveOverride(Clone(profile)); return; }
            if (transient) return;
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            string temp = PathName + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(profile, true));
            if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak");
            else File.Move(temp, PathName);
        }

        private static bool TryCommit(PlayerProfile candidate)
        {
            try { Validate(candidate); Persist(candidate); }
            catch (Exception ex)
            {
                Notice = "保存失败，操作未生效：" + ex.GetType().Name + "。请确认存档目录可写后重试。";
                return false;
            }
            CopyInto(candidate, Current);
            if (Notice != null && (Notice.StartsWith("保存失败", StringComparison.Ordinal) || Notice.StartsWith("奖励数值", StringComparison.Ordinal))) Notice = "";
            revision++; PublishChanged(); return true;
        }

        private static PlayerProfile Clone(PlayerProfile source)
        {
            var copy = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(source));
            Validate(copy); return copy;
        }

        private static void CopyInto(PlayerProfile source, PlayerProfile target)
        {
            target.schemaVersion = source.schemaVersion; target.contentVersion = source.contentVersion;
            target.tickets = source.tickets; target.fragments = source.fragments; target.mindset = source.mindset;
            target.loadout = source.loadout; target.unlocked = source.unlocked; target.achievements = source.achievements;
            target.practice = source.practice; target.questFacts = source.questFacts; target.claimedQuests = source.claimedQuests;
        }

        private static void PublishChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public static bool RecordFact(string key, int value) { return Progress.RecordFact(key, value); }
        public static bool AddFact(string key, int amount) { return Progress.AddFact(key, amount); }
        public static bool ClaimQuest(string id) { return Progress.ClaimQuest(id); }
        public static bool Reward(string achievement, int tickets = 1, string incrementFact = null) { return Progress.GrantLegacy(achievement, tickets, incrementFact); }

        public static bool RecordPractice(string routeId, int score)
        {
            Load();
            if (Array.IndexOf(BattleContent.Ids, routeId) < 0) throw new ArgumentException("Unknown route");
            var service = Progress;
            var candidate = Clone(Current);
            var record = candidate.practice.Find(r => r.routeId == routeId);
            if (record == null) { record = new PracticeRecord { routeId = routeId }; candidate.practice.Add(record); }
            record.attempts = checked(record.attempts + 1);
            record.lastScore = Math.Max(0, Math.Min(100, score)); record.bestScore = Math.Max(record.bestScore, record.lastScore);
            var change = service.PrepareFacts(ReadProgress(candidate), new[] {
                new QuestFactRecord { key = "practice.best", value = record.lastScore },
                new QuestFactRecord { key = "practice.best." + routeId, value = record.lastScore }
            });
            try { ApplyProgress(candidate, change.State, change.Grants); }
            catch (OverflowException) { Notice = "奖励数值超过存档上限，操作未生效。"; return false; }
            return TryCommit(candidate);
        }

        public static bool TryEquipTechnique(string id, int slot)
        {
            Load();
            if (slot < 0 || slot >= Current.loadout.Length || Array.IndexOf(BattleContent.Ids, id) < 0 || !Current.unlocked.Contains(id)) return false;
            if (Current.loadout[slot] == id) return true;
            var candidate = Clone(Current);
            int previousSlot = Array.IndexOf(candidate.loadout, id);
            string previous = candidate.loadout[slot]; candidate.loadout[slot] = id;
            if (previousSlot >= 0) candidate.loadout[previousSlot] = previous;
            return TryCommit(candidate);
        }

        public static bool TrySetMindset(string mindset)
        {
            Load();
            if (mindset != "shouzhuo" && mindset != "fanzhao") return false;
            if (Current.mindset == mindset) return true;
            var candidate = Clone(Current); candidate.mindset = mindset; return TryCommit(candidate);
        }

        public static string Draw(int random)
        {
            Load();
            if (Current.tickets <= 0) return "没有签令：首胜、练功与遗迹试炼可以获得。";
            var candidate = Clone(Current); candidate.tickets--;
            var locked = new List<string>();
            foreach (var id in BattleContent.Ids) if (!candidate.unlocked.Contains(id)) locked.Add(id);
            string result;
            if (locked.Count > 0)
            {
                string id = locked[(random & int.MaxValue) % locked.Count]; candidate.unlocked.Add(id);
                result = "获得秘籍 · " + BattleContent.Name(id);
            }
            else { candidate.fragments = checked(candidate.fragments + 1); result = "秘籍已集齐，获得1碎片。"; }
            return TryCommit(candidate) ? result : "保存失败，未消耗签令。请检查存档目录后重试。";
        }

        private static QuestProgressState ReadProgress(PlayerProfile profile)
        {
            var state = new QuestProgressState();
            foreach (var fact in profile.questFacts) state.Facts.Add(fact.Copy());
            state.ClaimedQuestIds.AddRange(profile.claimedQuests);
            state.LegacyAchievements.AddRange(profile.achievements);
            return state;
        }

        private static void ApplyProgress(PlayerProfile candidate, QuestProgressState state, IReadOnlyList<QuestRewardGrant> grants)
        {
            candidate.questFacts = new List<QuestFactRecord>();
            foreach (var fact in state.Facts) candidate.questFacts.Add(fact.Copy());
            candidate.claimedQuests = new List<string>(state.ClaimedQuestIds);
            candidate.achievements = new List<string>(state.LegacyAchievements);
            foreach (var grant in grants)
            {
                candidate.tickets = checked(candidate.tickets + grant.Reward.Tickets);
                candidate.fragments = checked(candidate.fragments + grant.Reward.Fragments);
                foreach (var id in grant.Reward.UnlockTechniqueIds) if (!candidate.unlocked.Contains(id)) candidate.unlocked.Add(id);
            }
        }

        private sealed class ProfileQuestRepository : IQuestProgressRepository
        {
            private readonly object owner;
            public ProfileQuestRepository(object token) { owner = token; }
            public long Revision { get { EnsureOwner(); return revision; } }
            private void EnsureOwner()
            {
                if (!ReferenceEquals(owner, profileToken)) throw new InvalidOperationException("此任务服务属于另一个存档会话。");
            }
            public QuestProgressState Read() { EnsureOwner(); return ReadProgress(Current); }
            public bool TryCommit(QuestProgressState next, IReadOnlyList<QuestRewardGrant> grants)
            {
                EnsureOwner();
                var candidate = Clone(Current);
                try { ApplyProgress(candidate, next, grants); }
                catch (OverflowException) { Notice = "奖励数值超过存档上限，操作未生效。"; return false; }
                return ProfileStore.TryCommit(candidate);
            }
        }
    }
}
