using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Yibi.Progression;
using Yibi.Rules;
using Yibi.UI;
using Yibi.World;

namespace Yibi.Editor
{
    /// <summary>Explicit authoring command. All fixed UI, callbacks and content assets are saved in Unity.</summary>
    public static class StageBSetup
    {
        const string ContentRoot = "Assets/_Game/Resources/Progression";
        const string DialogueRoot = "Assets/_Game/Content/Dialogue";
        const string PrefabRoot = "Assets/_Game/Prefabs/UI";
        static readonly Color Ink = new Color(.035f, .075f, .09f, .97f);
        static readonly Color SolidInk = new Color(.035f, .075f, .09f, 1f);
        static readonly Color Card = new Color(.065f, .115f, .125f, .98f);
        static readonly Color Paper = new Color(.94f, .91f, .81f);
        static readonly Color Muted = new Color(.66f, .74f, .72f);
        static readonly Color Gold = new Color(.76f, .61f, .36f);
        static Font font;
        static SystemFontProfile fontProfile;

        [MenuItem("一笔江湖/3.0/阶段B/构建任务验证版")]
        public static void Build()
        {
            PolishEnvironmentSetup.Guard();
            var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalogSO>(ContentRoot + "/QuestCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("先保存阶段 B 任务内容，再执行构建。");
            var errors = catalog.Validate();
            if (errors.Length != 0) throw new InvalidOperationException("任务配置校验失败：\n" + string.Join("\n", errors));
            ContentExporter.Export();
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            var output = Path.Combine(root, "Builds/FrameworkStageB");
            var evidence = Path.Combine(root, "Docs/Evidence");
            Directory.CreateDirectory(output); Directory.CreateDirectory(evidence);
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, Path.Combine(output, "YibiJianghu.exe"),
                BuildTarget.StandaloneWindows64, BuildOptions.Development);
            File.WriteAllText(Path.Combine(evidence, "FrameworkStageB-build.txt"),
                "utc=" + DateTime.UtcNow.ToString("o") + "\nresult=" + report.summary.result +
                "\nerrors=" + report.summary.totalErrors + "\nwarnings=" + report.summary.totalWarnings +
                "\nbytes=" + report.summary.totalSize + "\nseconds=" + report.summary.totalTime.TotalSeconds);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("阶段 B 构建失败，请查看构建报告。");
        }

        [MenuItem("一笔江湖/3.0/阶段B/保存任务内容与归云谷界面")]
        public static void Install()
        {
            PolishEnvironmentSetup.Guard();
            foreach (var dir in new[] {ContentRoot, ContentRoot + "/Definitions", ContentRoot + "/Rewards", DialogueRoot, PrefabRoot})
                Directory.CreateDirectory(dir);
            Directory.CreateDirectory("Assets/_Game/Content/UI");
            AssetDatabase.Refresh();
            fontProfile = CreateIfMissing<SystemFontProfile>("Assets/_Game/Content/UI/GuiyunSystemFontProfile.asset", value => {
                value.families = new[] {"Microsoft YaHei", "SimHei", "Arial"}; value.defaultSize = 24;
            });
            font = SavedChineseFont.GetSharedFont(fontProfile);
            SeedContent();
            SaveRowPrefab();
            SaveValley();
            AssetDatabase.SaveAssets();
        }

        static T CreateIfMissing<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing; // Inspector edits are the source of truth after first creation.
            var value = ScriptableObject.CreateInstance<T>();
            initialize(value);
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        static QuestDefinitionSO Quest(string id, string title, string description, string target, int order,
            string fact, int required, string label, int tickets, int fragments, string legacy,
            bool main = true, bool automatic = true, QuestDefinitionSO[] prerequisites = null)
        {
            var reward = CreateIfMissing<RewardDefinitionSO>(ContentRoot + "/Rewards/" + id + ".asset", value => {
                value.stableId = "reward.guiyun." + id; value.tickets = tickets; value.fragments = fragments;
                value.unlockTechniqueIds = new string[0];
            });
            return CreateIfMissing<QuestDefinitionSO>(ContentRoot + "/Definitions/" + id + ".asset", value => {
                value.stableId = "guiyun." + id; value.title = title; value.description = description;
                value.targetId = target; value.legacyAchievementId = legacy; value.order = order;
                value.isMain = main; value.autoClaim = automatic; value.reward = reward;
                value.prerequisites = prerequisites ?? new QuestDefinitionSO[0];
                value.objectives = string.IsNullOrEmpty(fact) ? new QuestObjective[0] :
                    new[] {new QuestObjective {factKey = fact, requiredValue = required, label = label}};
            });
        }

        static void SeedContent()
        {
            var practice = Quest("practice", "初识经脉", "与听松先生交谈，在练功台亲手画出一条准确的经脉。", "practice", 10,
                "practice.best", 70, "手绘最高评分", 1, 0, "practice-pass");
            var bamboo = Quest("bamboo", "竹林试剑", "挑战竹林守卫，试试点穴与裂石掌的衔接。", "bamboo", 20,
                "win.0", 1, "战胜竹林守卫", 1, 0, "first-win-0");
            var trial = Quest("trial", "听风疾行", "前往听风碑，按顺序穿过标记，完成疾行试炼。", "trial", 30,
                "trial.complete", 1, "完成疾行试炼", 2, 0, "valley-trial");
            var final = Quest("final", "问心一战", "通过竹林与疾行考验后，前往问心台挑战遗迹守卫。", "final", 40,
                "win.1", 1, "战胜遗迹守卫", 1, 0, "first-win-1");
            var chapter = Quest("chapter", "归云卷成", "完成初识经脉、竹林试剑、听风疾行与问心一战。", "inventory", 50,
                null, 0, null, 3, 0, "chapter-one", true, true, new[] {practice, bamboo, trial, final});
            var exam = Quest("exam", "六脉贯通", "前往练功台，连续通过六条经脉的进阶考核。", "practice", 60,
                "exam.complete", 1, "完成六脉考核", 3, 0, "meridian-exam", false);
            var caches = Quest("caches", "谷中寻珍", "探访归云谷三处江湖遗匣；寻齐后在此领取额外碎片。", "", 70,
                "cache.count", 3, "开启江湖遗匣", 0, 2, "", false, false);
            var catalog = CreateIfMissing<QuestCatalogSO>(ContentRoot + "/QuestCatalog.asset", value => {
                value.quests = new[] {practice, bamboo, trial, final, chapter, exam, caches};
                value.legacyFactMappings = new[] {new LegacyFactMapping {
                    factKey = "cache.count", achievementIds = new[] {"cache-guiyun-1", "cache-guiyun-2", "cache-guiyun-3"}
                }};
            });
            var errors = catalog.Validate();
            if (errors.Length != 0) throw new InvalidOperationException("任务配置校验失败：\n" + string.Join("\n", errors));

            Dialogue("practice", "听松先生 · 初识经脉", "一笔相连，意先于形。\n从第一处穴位依序收笔，先求准确，再求熟练。\n\n练功目标与奖励，可随时在任务日志中查看。", "前往练功");
            Dialogue("bamboo", "竹林守卫 · 试剑", "点穴留下破绽，裂石掌便能借势增伤。\n别耗尽内力：调息也是攻防的一部分。\n\n准备好后，来切磋一场。", "开始切磋");
            Dialogue("trial", "听风碑 · 疾行试炼", "依次穿过五个亮起的标记，在时限内抵达终点。\n按住左 Shift 奔跑，沿北侧小径前行。\n\n留意上方的倒计时与下一处金色标记。", "开始试炼");
            Dialogue("final", "遗迹守卫 · 问心", "我会用回风积势，再以流火追击。\n护体能抵挡伤害，清心可驱散灼伤。\n\n胜过我，便是归云卷的最后一场考验。", "开始切磋",
                new[] {"guiyun.bamboo", "guiyun.trial"}, "先完成「竹林试剑」与「听风疾行」。\n\n通过这两项考验，再来问心台挑战。");
            Dialogue("inventory", "整理行囊", "选择秘籍，将其装入三个招式槽。", "打开行囊");
        }

        static DialogueDefinitionSO Dialogue(string id, string speaker, string body, string accept,
            string[] required = null, string blocked = "")
        {
            return CreateIfMissing<DialogueDefinitionSO>(DialogueRoot + "/" + id + ".asset", value => {
                value.speaker = speaker; value.body = body; value.acceptLabel = accept;
                value.blockedBody = blocked; value.requiredQuestIds = required ?? new string[0];
            });
        }

        static void SaveRowPrefab()
        {
            string path = PrefabRoot + "/QuestRow.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) {
                // Migrate the binding without replacing designer changes to the saved row layout.
                var contents = PrefabUtility.LoadPrefabContents(path);
                try {
                    var binding = contents.GetComponent<SavedChineseFont>();
                    if (binding == null) binding = contents.AddComponent<SavedChineseFont>();
                    binding.profile = fontProfile; binding.Apply();
                    EditorUtility.SetDirty(binding);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                } finally { PrefabUtility.UnloadPrefabContents(contents); }
                return;
            }
            var root = Panel("任务条目", null, Vector2.zero, new Vector2(994, 184), Card);
            try {
                var minimum = root.gameObject.AddComponent<LayoutElement>(); minimum.minHeight = 184;
                var columns = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                columns.padding = new RectOffset(24, 22, 17, 17); columns.spacing = 26;
                columns.childControlWidth = columns.childControlHeight = true;
                columns.childForceExpandWidth = false; columns.childForceExpandHeight = true;
                var row = root.gameObject.AddComponent<QuestRowView>();
                row.accent = Panel("状态侧线", root.transform, new Vector2(-493, 0), new Vector2(3, 156), Gold);
                row.accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                row.accent.rectTransform.anchorMin = new Vector2(0, 0); row.accent.rectTransform.anchorMax = new Vector2(0, 1);
                row.accent.rectTransform.offsetMin = new Vector2(2, 14); row.accent.rectTransform.offsetMax = new Vector2(5, -14);
                var left = VerticalColumn("任务信息", root.transform, 666);
                left.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                row.title = Label("任务标题", left, "初识经脉", Vector2.zero, new Vector2(666, 32), 23, Paper);
                var stateBar = ArenaSetup.Rect("任务分类与状态", left, Vector2.zero, new Vector2(666, 24));
                var stateLayout = stateBar.gameObject.AddComponent<HorizontalLayoutGroup>();
                stateLayout.childControlWidth = stateLayout.childControlHeight = true;
                stateLayout.spacing = 18; stateLayout.childForceExpandWidth = false; stateLayout.childForceExpandHeight = true;
                stateBar.gameObject.AddComponent<LayoutElement>().minHeight = 24;
                row.category = Label("主支线", stateBar, "主线", Vector2.zero, new Vector2(62, 24), 14, Gold);
                row.state = Label("任务状态", stateBar, "进行中", Vector2.zero, new Vector2(164, 24), 14, Gold);
                row.description = Label("任务说明", left, "与听松先生交谈，在练功台亲手画出一条准确的经脉。", Vector2.zero, new Vector2(666, 44), 16, Paper);
                row.progress = Label("目标进度", left, "手绘最高评分  0 / 70", Vector2.zero, new Vector2(666, 25), 15, Muted);
                var right = VerticalColumn("任务奖励", root.transform, 250);
                var rightSize = right.gameObject.AddComponent<LayoutElement>(); rightSize.preferredWidth = 250; rightSize.minWidth = 250;
                right.GetComponent<VerticalLayoutGroup>().spacing = 16;
                row.reward = Label("奖励", right, "奖励 · 1 签令", Vector2.zero, new Vector2(250, 64), 18, Gold, TextAnchor.MiddleCenter);
                row.reward.gameObject.AddComponent<LayoutElement>().minHeight = 50;
                row.claimButton = Button("领取任务奖励", right, "领取奖励", Vector2.zero, new Vector2(250, 46), true);
                row.claimButton.gameObject.AddComponent<LayoutElement>().minHeight = 46;
                row.claimLabel = row.claimButton.GetComponentInChildren<Text>();
                root.raycastTarget = false;
                var fontBinding = root.gameObject.AddComponent<SavedChineseFont>(); fontBinding.profile = fontProfile; fontBinding.Apply();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            } finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        static void SaveValley()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");
            var valley = UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
            if (valley == null || valley.journey == null) throw new InvalidOperationException("归云谷缺少现有探索组件；没有覆盖场景。");
            var root = valley.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(root), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var child in root.transform.Cast<Transform>().ToArray())
                if (child is RectTransform) UnityEngine.Object.DestroyImmediate(child.gameObject);
            root.name = "ValleyHUD · 归云卷";
            var oldFont = root.GetComponent<LocalChineseFont>();
            if (oldFont != null) UnityEngine.Object.DestroyImmediate(oldFont);
            if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();
            var journal = root.GetComponent<QuestJournalPresenter>();
            if (journal == null) journal = root.AddComponent<QuestJournalPresenter>();
            journal.valley = valley; valley.journal = journal;
            var journey = valley.journey;
            SaveWorldBindings(valley);
            Hud(valley, journey, journal);
            Inventory(valley, journey);
            DialoguePanel(journey);
            JournalPanel(journal);
            PausePanel(valley);
            valley.inventoryPanel.SetActive(false); valley.pausePanel.SetActive(false);
            journey.dialoguePanel.SetActive(false); journal.panel.SetActive(false);
            var fontBinding = root.GetComponent<SavedChineseFont>();
            if (fontBinding == null) fontBinding = root.AddComponent<SavedChineseFont>();
            fontBinding.profile = fontProfile; fontBinding.Apply();
            EditorUtility.SetDirty(valley); EditorUtility.SetDirty(journey); EditorUtility.SetDirty(journal);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabRoot + "/StageBValleyHUD.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        static void SaveWorldBindings(ValleyPresenter valley)
        {
            string[] ids = {"practice", "bamboo", "final", "trial", "inventory"};
            string[] names = {"练功台", "竹林守卫", "遗迹守卫", "听风碑", "江湖行囊"};
            UnityAction[] actions = {valley.GoToPractice, valley.ChallengeBamboo, valley.ChallengeFinal, valley.StartTrial, valley.ToggleInventory};
            if (valley.interactionPoints.Length != ids.Length) throw new InvalidOperationException("已有地标布局不匹配初始迁移，请手动绑定 WorldInteraction。");
            valley.interactions = new WorldInteraction[ids.Length];
            for (int i = 0; i < ids.Length; i++) {
                var component = valley.interactionPoints[i].GetComponent<WorldInteraction>();
                if (component == null) {
                    component = valley.interactionPoints[i].gameObject.AddComponent<WorldInteraction>();
                    component.stableId = ids[i]; component.displayName = names[i];
                    component.dialogue = AssetDatabase.LoadAssetAtPath<DialogueDefinitionSO>(DialogueRoot + "/" + ids[i] + ".asset");
                    UnityEventTools.AddPersistentListener(component.onAccepted, actions[i]);
                }
                valley.interactions[i] = component;
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        static void Hud(ValleyPresenter p, JourneyPresenter j, QuestJournalPresenter journal)
        {
            var top = Panel("旅程顶栏", p.transform, new Vector2(0, 312), new Vector2(1240, 72), Ink);
            Label("卷名", top.transform, "一 笔 江 湖", new Vector2(-503, 8), new Vector2(194, 36), 23, Paper);
            Label("章名", top.transform, "归云第一章", new Vector2(-503, -18), new Vector2(194, 25), 12, Gold);
            p.region = Label("当前区域", top.transform, "村落 · 归云谷", new Vector2(-155, 0), new Vector2(455, 46), 22, Paper);
            var log = Button("任务日志入口", top.transform, "任务日志  [J]", new Vector2(332, 0), new Vector2(172, 45), false);
            UnityEventTools.AddPersistentListener(log.onClick, journal.Toggle);
            var bag = Button("行囊入口", top.transform, "秘籍行囊  [Tab]", new Vector2(514, 0), new Vector2(176, 45), false);
            UnityEventTools.AddPersistentListener(bag.onClick, p.ToggleInventory);
            var quest = Panel("当前任务追踪", p.transform, new Vector2(-411, 174), new Vector2(398, 174), Ink);
            Panel("任务题线", quest.transform, new Vector2(-193, 0), new Vector2(3, 140), Gold);
            j.chapter = Label("追踪标题", quest.transform, "初识经脉", new Vector2(8, 60), new Vector2(354, 34), 22, Paper);
            j.objective = Label("追踪目标", quest.transform, "手绘最高评分  0 / 70", new Vector2(8, -1), new Vector2(354, 74), 17, Muted);
            j.compass = Label("追踪方向", quest.transform, "北 · 西   8 米", new Vector2(8, -65), new Vector2(354, 26), 15, Gold);
            var map = Panel("归云谷导览", p.transform, new Vector2(516, 173), new Vector2(192, 186), Ink);
            Label("导览题签", map.transform, "北 ↑   归云谷", new Vector2(0, 73), new Vector2(162, 30), 16, Gold, TextAnchor.MiddleCenter);
            var mapArea = ArenaSetup.Rect("地标绘图区", map.transform, new Vector2(0, -11), new Vector2(168, 138));
            j.mapTargets = new RectTransform[p.interactionPoints.Length];
            for (int i = 0; i < j.mapTargets.Length; i++) {
                var marker = Panel("地标_" + i, mapArea, Vector2.zero, Vector2.one * 8, Gold);
                marker.raycastTarget = false; j.mapTargets[i] = marker.rectTransform;
            }
            var player = Panel("玩家位置", mapArea, Vector2.zero, Vector2.one * 10, new Color(.40f, .87f, .77f));
            player.raycastTarget = false; j.mapPlayer = player.rectTransform;
            j.collection = Label("收集进度", p.transform, "遗匣 0 / 3", new Vector2(466, 61), new Vector2(290, 28), 14, Paper, TextAnchor.MiddleRight);
            p.trialText = Label("疾行计时", p.transform, "", new Vector2(0, 239), new Vector2(700, 42), 23, Gold, TextAnchor.MiddleCenter);
            j.treasureHint = Label("遗匣交互", p.transform, "", new Vector2(0, -213), new Vector2(800, 38), 21, Gold, TextAnchor.MiddleCenter);
            p.prompt = Label("交互提示", p.transform, "沿山道寻找练功台、竹林和遗迹", new Vector2(0, -262), new Vector2(1130, 44), 22, Paper, TextAnchor.MiddleCenter);
            var footer = Panel("操作与消息", p.transform, new Vector2(0, -321), new Vector2(1240, 62), Ink);
            p.notice = Label("消息正文", footer.transform, "WASD 行走 · Shift 奔跑 · E 交谈 · F 开匣 · J 任务 · Tab 行囊", Vector2.zero, new Vector2(1192, 48), 16, Muted, TextAnchor.MiddleCenter);
        }

        static void Inventory(ValleyPresenter p, JourneyPresenter j)
        {
            var modal = Modal("秘籍行囊", p.transform, 20); p.inventoryPanel = modal.gameObject;
            var body = Panel("行囊正文", modal.transform, Vector2.zero, new Vector2(1116, 610), SolidInk);
            Label("行囊标题", body.transform, "秘籍行囊", new Vector2(-357, 255), new Vector2(322, 54), 30, Paper);
            Label("行囊副题", body.transform, "选一本秘籍，定三招应敌。", new Vector2(250, 255), new Vector2(510, 40), 16, Gold, TextAnchor.MiddleRight);
            Line(body.transform, 215, 1030);
            p.profileText = Label("行囊记录与指引", body.transform, "", new Vector2(0, 163), new Vector2(1028, 96), 16, Muted);
            j.skillCards = new Image[BattleContent.Ids.Length];
            for (int i = 0; i < j.skillCards.Length; i++) {
                var card = Button("秘籍卡_" + BattleContent.Ids[i], body.transform, BattleContent.Names[i], new Vector2(-430 + i * 172, 70), new Vector2(160, 62), false);
                UnityEventTools.AddIntPersistentListener(card.onClick, j.SelectTechnique, i); j.skillCards[i] = card.image;
            }
            j.techniqueDetail = Label("选中秘籍说明", body.transform, "", new Vector2(0, -9), new Vector2(1028, 94), 18, Paper);
            j.equipSelected = new Button[3]; p.equipButtons = new Button[3];
            for (int i = 0; i < j.equipSelected.Length; i++) {
                var slot = Button("装配槽_" + (i + 1), body.transform, "槽 " + (i + 1), new Vector2(-345 + i * 345, -93), new Vector2(328, 50), true);
                UnityEventTools.AddIntPersistentListener(slot.onClick, j.EquipTechnique, i);
                j.equipSelected[i] = slot; p.equipButtons[i] = slot;
            }
            var mind = Button("切换心法", body.transform, "心法 · 守拙", new Vector2(-192, -161), new Vector2(652, 48), false);
            p.mindsetLabel = mind.GetComponentInChildren<Text>(); UnityEventTools.AddPersistentListener(mind.onClick, p.SwitchMindset);
            var draw = Button("签令寻诀", body.transform, "消耗 1 签令 · 寻诀", new Vector2(346, -161), new Vector2(328, 48), false);
            UnityEventTools.AddPersistentListener(draw.onClick, p.Draw);
            p.inventoryNotice = Label("行囊操作反馈", body.transform, "", new Vector2(0, -213), new Vector2(1028, 36), 16, Gold, TextAnchor.MiddleCenter);
            var close = Button("关闭行囊", body.transform, "收起行囊  [Tab]", new Vector2(0, -254), new Vector2(286, 45), false);
            UnityEventTools.AddPersistentListener(close.onClick, p.ToggleInventory);
            modal.GetComponent<ModalFocusScope>().defaultSelection = close;
        }

        static void DialoguePanel(JourneyPresenter j)
        {
            var modal = Modal("江湖对话", j.transform, 30); j.dialoguePanel = modal.gameObject;
            var body = Panel("对话正文", modal.transform, new Vector2(0, -118), new Vector2(1040, 356), SolidInk);
            j.dialogueTitle = Label("说话人", body.transform, "听松先生 · 初识经脉", new Vector2(0, 128), new Vector2(946, 48), 27, Gold);
            Line(body.transform, 91, 946);
            j.dialogueBody = Label("对话内容", body.transform, "", new Vector2(0, -2), new Vector2(946, 160), 21, Paper);
            var cancel = Button("暂别", body.transform, "稍后再来", new Vector2(-190, -128), new Vector2(320, 48), false);
            UnityEventTools.AddPersistentListener(cancel.onClick, j.CloseDialogue);
            j.accept = Button("接受交互", body.transform, "前往练功", new Vector2(190, -128), new Vector2(320, 48), true);
            UnityEventTools.AddPersistentListener(j.accept.onClick, j.AcceptDialogue);
            modal.GetComponent<ModalFocusScope>().defaultSelection = cancel;
        }

        static void JournalPanel(QuestJournalPresenter journal)
        {
            var modal = Modal("任务日志", journal.transform, 40); journal.panel = modal.gameObject;
            var body = Panel("任务日志正文", modal.transform, Vector2.zero, new Vector2(1116, 630), SolidInk);
            Label("日志标题", body.transform, "江湖手札", new Vector2(-350, 270), new Vector2(332, 54), 30, Paper);
            journal.summary = Label("旅程概览", body.transform, "已完成 0 / 7", new Vector2(253, 264), new Vector2(520, 40), 16, Gold, TextAnchor.MiddleRight);
            Label("日志说明", body.transform, "循着主线历练，也别错过山谷中的支线。可领取的奖励会在条目右侧亮起。", new Vector2(0, 220), new Vector2(1028, 36), 16, Muted);
            Line(body.transform, 194, 1028);
            var scrollRoot = Panel("可滚动任务列表", body.transform, new Vector2(0, -22), new Vector2(1028, 414), new Color(0, 0, 0, .05f));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            var viewport = Panel("可视区域", scrollRoot.transform, Vector2.zero, new Vector2(1008, 414), Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>(); scroll.viewport = viewport.rectTransform;
            var content = new GameObject("任务条目容器", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var rect = (RectTransform)content.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(7, 7, 0, 4); layout.spacing = 12;
            layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            journal.content = rect; scroll.content = rect;
            journal.rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/QuestRow.prefab").GetComponent<QuestRowView>();
            var track = Panel("滚动条", scrollRoot.transform, new Vector2(509, 0), new Vector2(4, 414), new Color(.16f, .22f, .23f));
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Panel("滑块", track.transform, Vector2.zero, new Vector2(4, 100), Gold);
            Stretch(handle.rectTransform);
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle; scroll.verticalScrollbar = scrollbar;
            var close = Button("关闭任务日志", body.transform, "收起手札  [J]", new Vector2(0, -275), new Vector2(286, 46), false);
            UnityEventTools.AddPersistentListener(close.onClick, journal.Close);
            modal.GetComponent<ModalFocusScope>().defaultSelection = close;
        }

        static void PausePanel(ValleyPresenter p)
        {
            var modal = Modal("探索菜单", p.transform, 60); p.pausePanel = modal.gameObject;
            var body = Panel("菜单正文", modal.transform, Vector2.zero, new Vector2(470, 492), SolidInk);
            Label("菜单标题", body.transform, "歇脚片刻", new Vector2(0, 191), new Vector2(390, 64), 30, Paper, TextAnchor.MiddleCenter);
            string[] labels = {"继续探索", "双人论武", "返回主页", "保存并退出"};
            UnityAction[] actions = {p.Resume, p.OpenLobby, p.ReturnToTitle, p.Quit};
            for (int i = 0; i < labels.Length; i++) {
                var button = Button(labels[i], body.transform, labels[i], new Vector2(0, 101 - i * 72), new Vector2(374, 52), i == 0);
                UnityEventTools.AddPersistentListener(button.onClick, actions[i]);
                if (i == 0) modal.GetComponent<ModalFocusScope>().defaultSelection = button;
            }
        }

        static Image Modal(string name, Transform parent, int order)
        {
            var modal = Panel(name, parent, Vector2.zero, new Vector2(1280, 720), new Color(0, 0, 0, .62f));
            Stretch(modal.rectTransform); modal.gameObject.AddComponent<ModalLayer>().order = order;
            modal.gameObject.AddComponent<ModalFocusScope>().backgroundGroup = parent.GetComponent<CanvasGroup>();
            return modal;
        }
        static RectTransform VerticalColumn(string name, Transform parent, float width)
        {
            var rect = ArenaSetup.Rect(name, parent, Vector2.zero, new Vector2(width, 150));
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>(); group.spacing = 6;
            group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleLeft; return rect;
        }
        static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color) { return ArenaSetup.Panel(name, parent, pos, size, color); }
        static Text Label(string name, Transform parent, string value, Vector2 pos, Vector2 size, int points, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var label = ArenaSetup.Label(name, parent, value, pos, size, points); label.font = font; label.color = color;
            label.alignment = anchor; label.raycastTarget = false; return label;
        }
        static Button Button(string name, Transform parent, string value, Vector2 pos, Vector2 size, bool primary)
        {
            var button = ArenaSetup.Button(name, parent, value, pos, size); button.image.color = primary ? Gold : new Color(.12f, .18f, .19f);
            var colors = button.colors; colors.highlightedColor = new Color(1.1f, 1.1f, 1.05f); colors.pressedColor = new Color(.72f, .78f, .74f); colors.disabledColor = new Color(.48f, .53f, .51f); button.colors = colors;
            var label = button.GetComponentInChildren<Text>(); label.font = font; label.fontSize = 18; label.color = primary ? Ink : Paper; return button;
        }
        static void Line(Transform parent, float y, float width) { Panel("金线", parent, new Vector2(0, y), new Vector2(width, 1), new Color(Gold.r, Gold.g, Gold.b, .38f)); }
        static void Stretch(RectTransform value) { value.anchorMin = Vector2.zero; value.anchorMax = Vector2.one; value.offsetMin = value.offsetMax = Vector2.zero; }
    }
}
