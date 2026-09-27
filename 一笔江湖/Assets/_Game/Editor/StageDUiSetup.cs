using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Battle;
using Yibi.UI;
using Yibi.World;

namespace Yibi.Editor
{
    /// <summary>One-time role authoring, followed by repeatable skin application. Never rebuilds a HUD.</summary>
    public static class StageDUiSetup
    {
        public const string Folder = "Assets/_Game/Art/Selected/StageD/UI";
        public const string ProfilePath = "Assets/_Game/Content/UI/GuiyunUISkin.asset";
        const string Source = "Assets/_Game/资源/C1869/UI/UI切图/";
        const string FrameName = "资源金边 · 外观";
        const string QuestName = "资源卷轴 · 手札";
        const string LobbyBackdrop = "资源暗纹 · 论武背景";

        [Serializable] sealed class SourceEntry { public string source, selected, sourceSha256, selectedSha256; }
        [Serializable] sealed class SourceManifest { public string note; public List<SourceEntry> files = new List<SourceEntry>(); }
        static readonly HashSet<string> PaperNames = new HashSet<string> {
            "墨色导航", "指引正文", "行囊正文", "对话正文", "任务日志正文", "菜单正文", "胜败结算"
        };
        static readonly HashSet<string> DarkNames = new HashSet<string> {
            "右下题签", "旅程顶栏", "当前任务追踪", "归云谷导览", "操作与消息", "任务条目",
            "战局顶栏", "观势手札", "本回合与战斗记录", "行动与构筑", "构筑提要", "经脉施法面板", "联机操作",
            "秘籍选择面板", "评分面板", "服务器地址", "房间码", "工作台说明底板", "行程加载", LobbyBackdrop
        };
        static readonly HashSet<string> PrimaryNames = new HashSet<string> {
            "继续旅程", "确认施放", "接受交互", "领取任务奖励", "准备", "创建房间", "再战", "返回主页", "继续探索"
        };
        static readonly string[] Prefabs = {
            "Assets/_Game/Prefabs/MainMenuUI.prefab", "Assets/_Game/Prefabs/RoomHUD.prefab",
            "Assets/_Game/Prefabs/GestureLabUI.prefab", "Assets/_Game/Prefabs/UI/StageBValleyHUD.prefab",
            "Assets/_Game/Prefabs/UI/StageCBattleHUD.prefab", "Assets/_Game/Prefabs/UI/QuestRow.prefab",
            "Assets/_Game/Resources/App/GameRoot.prefab"
        };

        [MenuItem("一笔江湖/3.0/阶段D/应用购买资源界面外观")]
        public static void Install()
        {
            PolishEnvironmentSetup.Guard();
            if (new DirectoryInfo(Directory.GetParent(Application.dataPath).FullName).Name != "一笔江湖")
                throw new InvalidOperationException("只允许在独立的一笔江湖工程应用外观。");
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var profile = PrepareAssets();
            // Persist the newly created SO before opening another scene can unload its first instance.
            AssetDatabase.SaveAssets();
            if (profile.ValidateConfiguration().Length != 0)
                throw new InvalidOperationException("外观配置不完整，请修正 Inspector 中的引用。");
            try {
                foreach (string path in Prefabs) {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try {
                        ApplyToRoot(root, profile);
                        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Prefab 保存失败：" + path);
                    } finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (string name in new[] { "MainMenu", "Valley", "Arena_Stone", "Arena_Bamboo", "GestureLab", "Lobby", "AnimationWorkshop" }) {
                    string path = "Assets/_Game/Scenes/" + name + ".unity";
                    if (!File.Exists(path)) continue;
                    var scene = EditorSceneManager.OpenScene(path);
                    var visited = new HashSet<GameObject>();
                    foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) {
                        if (canvas.renderMode == RenderMode.WorldSpace ||
                            (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null)) continue;
                        var inherited = canvas.GetComponentInParent<UISkinBinding>();
                        var owner = inherited == null ? canvas.gameObject : inherited.gameObject;
                        if (visited.Add(owner)) ApplyToRoot(owner, profile);
                    }
                    // No SaveAsPrefabAssetAndConnect here: scene actor / FX references remain scene overrides.
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("场景保存失败：" + path);
                    // A successful SaveScene is insufficient: inspect the serialized state after reload.
                    EditorSceneManager.OpenScene(path);
                    foreach (var binding in UnityEngine.Object.FindObjectsOfType<UISkinBinding>(true)) AssertApplied(binding);
                }
                AssetDatabase.SaveAssets();
            } finally { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UISkinProfile>(ProfilePath);
            Debug.Log("Stage D UI: purchased PNGs, explicit saved visual bindings and persistent callbacks retained.");
        }

        static UISkinProfile PrepareAssets()
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Assets/_Game/Content/UI"); AssetDatabase.Refresh();
            var manifest = new SourceManifest { note = "原样精选用户购买资源；只配置导入器九宫格，不绘制或重采样位图。未采用原项目 logo、角色肖像或商业标志。" };
            var paper = Select("UI_Bg_popup_dialogue.png", "PaperPanel.png", new Vector4(28, 28, 28, 28), manifest);
            var dark = Select("UI_Bg_content.png", "InkPanel.png", new Vector4(8, 8, 8, 8), manifest);
            var header = Select("UI_frame_bg_title.png", "KnottedHeader.png", new Vector4(48, 0, 48, 0), manifest);
            var button = Select("UI_btn_basic.png", "Button.png", new Vector4(8, 8, 8, 8), manifest);
            var frame = Select("UI_frame_bg_01.png", "GoldFrame.png", new Vector4(29, 12, 29, 12), manifest);
            var quest = Select("UI_Btn_Quest.png", "QuestScroll.png", Vector4.zero, manifest);
            string[] sources = { "Icon_Rune_6.png", "Icon_Rune_PenetrationDamage.png", "Icon_Rune_4.png", "Icon_Rune_AddFire.png", "Icon_Rune_2.png", "Icon_Rune_Health.png" };
            string[] ids = { "dianxue", "lieshi", "huifeng", "liuhuo", "huti", "qingxin" };
            var icons = new Sprite[6];
            for (int i = 0; i < icons.Length; i++) icons[i] = Select(sources[i], "Technique_" + ids[i] + ".png", Vector4.zero, manifest);
            string projectRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
            Directory.CreateDirectory(Path.Combine(projectRoot, "Docs/Evidence"));
            File.WriteAllText(Path.Combine(projectRoot, "Docs/Evidence/StageD-ui-source-manifest.json"), JsonUtility.ToJson(manifest, true));
            var profile = AssetDatabase.LoadAssetAtPath<UISkinProfile>(ProfilePath);
            if (profile != null) return profile; // Inspector references and palette remain the source of truth.
            profile = ScriptableObject.CreateInstance<UISkinProfile>();
            profile.paperPanel = paper; profile.darkPanel = dark; profile.headerPattern = header;
            profile.button = button; profile.frame = frame; profile.questIcon = quest; profile.techniqueIcons = icons;
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        static Sprite Select(string sourceName, string selectedName, Vector4 border, SourceManifest manifest)
        {
            string source = Source + sourceName, selected = Folder + "/" + selectedName;
            if (!File.Exists(source)) throw new FileNotFoundException("购买切图缺失", source);
            if (File.Exists(selected)) { if (Hash(source) != Hash(selected)) throw new IOException("保留已改动的精选图，不自动覆盖：" + selected); }
            else File.Copy(source, selected);
            AssetDatabase.ImportAsset(selected, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(selected);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border; importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.isReadable = false; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            manifest.files.Add(new SourceEntry { source = source, selected = selected, sourceSha256 = Hash(source), selectedSha256 = Hash(selected) });
            return AssetDatabase.LoadAssetAtPath<Sprite>(selected);
        }
        static string Hash(string path) { using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }

        static void ApplyToRoot(GameObject root, UISkinProfile profile)
        {
            // Editor scene changes may invalidate an asset wrapper held only on the execution stack.
            // Resolve the persistent asset for each root, and fail instead of silently skipping Apply.
            profile = AssetDatabase.LoadAssetAtPath<UISkinProfile>(ProfilePath);
            if (profile == null || profile.ValidateConfiguration().Length != 0)
                throw new InvalidOperationException("界面外观配置无法解析：" + root.name);
            string before = CallbackSignature(root);
            var binding = root.GetComponent<UISkinBinding>();
            bool first = binding == null || binding.images == null || binding.images.Length == 0;
            if (binding == null) binding = root.AddComponent<UISkinBinding>();
            binding.profile = profile;
            if (first) AuthorRoles(root, binding);
            AuthorButtonTextRoles(binding);
            binding.Apply();
            AssertApplied(binding);
            foreach (var battle in root.GetComponentsInChildren<BattlePresenter>(true)) {
                battle.techniqueIcons = (Sprite[])profile.techniqueIcons.Clone(); Mark(battle);
                if (battle.skillIcons != null) for (int i = 0; i < battle.skillIcons.Length; i++)
                    if (battle.skillIcons[i] != null) { battle.skillIcons[i].sprite = profile.techniqueIcons[i]; battle.skillIcons[i].color = profile.lightText; Mark(battle.skillIcons[i]); }
            }
            foreach (var journey in root.GetComponentsInChildren<JourneyPresenter>(true)) {
                journey.lockedCard = profile.lockedCard; journey.normalCard = profile.normalCard; journey.selectedCard = profile.selectedCard; Mark(journey);
            }
            foreach (var practice in root.GetComponentsInChildren<GestureLabPresenter>(true)) {
                practice.selectedRouteColor = profile.selectedCard; practice.normalRouteColor = profile.normalCard; Mark(practice);
            }
            foreach (var slot in binding.images) if (slot != null && slot.target != null) {
                Mark(slot.target); var button = slot.target.GetComponent<Button>(); if (button != null) Mark(button);
            }
            foreach (var slot in binding.texts) if (slot != null && slot.target != null) Mark(slot.target);
            Mark(binding);
            if (CallbackSignature(root) != before) throw new InvalidOperationException("换肤意外改变了按钮回调：" + root.name);
        }

        static void AssertApplied(UISkinBinding binding)
        {
            var errors = binding.ValidateApplied();
            if (errors.Length != 0) throw new InvalidOperationException("外观未完整应用／保存：" + binding.name + "\n" + string.Join("\n", errors));
        }

        static void AuthorButtonTextRoles(UISkinBinding binding)
        {
            // Earlier authoring omitted inactive ancestors. Repair the saved slots without
            // rebuilding any modal, and leave all non-button semantic text roles untouched.
            var texts = new List<UISkinTextSlot>(binding.texts ?? new UISkinTextSlot[0]);
            foreach (var slot in binding.images) {
                if (slot == null || slot.target == null ||
                    (slot.role != UISkinImageRole.Button && slot.role != UISkinImageRole.PrimaryButton)) continue;
                var button = slot.target.GetComponent<Button>();
                if (button == null) continue;
                var role = slot.role == UISkinImageRole.PrimaryButton ? UISkinTextRole.PrimaryButtonLabel : UISkinTextRole.ButtonLabel;
                foreach (var label in button.GetComponentsInChildren<Text>(true)) {
                    if (label.GetComponentInParent<Button>(true) != button) continue;
                    var existing = texts.Where(t => t != null && t.target == label).ToArray();
                    if (existing.Length == 0) texts.Add(new UISkinTextSlot { target = label, role = role });
                    else foreach (var value in existing) value.role = role;
                }
            }
            binding.texts = texts.ToArray();
        }

        static void AuthorRoles(GameObject root, UISkinBinding binding)
        {
            if (root.GetComponent<LobbyPresenter>() != null && root.transform.Find(LobbyBackdrop) == null) {
                var backdrop = Decoration(LobbyBackdrop, root.transform);
                backdrop.rectTransform.anchorMin = backdrop.rectTransform.anchorMax = new Vector2(.5f, .5f);
                backdrop.rectTransform.sizeDelta = new Vector2(1140, 666); backdrop.rectTransform.anchoredPosition = Vector2.zero;
                backdrop.transform.SetAsFirstSibling();
            }
            var images = new List<UISkinImageSlot>(); var texts = new List<UISkinTextSlot>();
            var originalImages = root.GetComponentsInChildren<Image>(true);
            foreach (var image in originalImages) {
                if (image.name == FrameName || image.name == QuestName || image.name == "资源绳纹 · 题签") continue;
                if (image.GetComponentInParent<GestureBoard>(true) != null) continue;
                var button = image.GetComponent<Button>();
                if (button != null) {
                    bool primary = PrimaryNames.Contains(image.name);
                    images.Add(new UISkinImageSlot { target = image, role = primary ? UISkinImageRole.PrimaryButton : UISkinImageRole.Button });
                    AddFrame(image.transform, images);
                } else if (PaperNames.Contains(image.name)) {
                    images.Add(new UISkinImageSlot { target = image, role = UISkinImageRole.PaperPanel });
                } else if (DarkNames.Contains(image.name)) {
                    images.Add(new UISkinImageSlot { target = image, role = UISkinImageRole.DarkPanel });
                    if (image.rectTransform.sizeDelta.x > 100 && image.rectTransform.sizeDelta.y > 32) AddFrame(image.transform, images);
                }
            }
            // One visible textile motif per header; the actual header back stays opaque for readability.
            foreach (var image in originalImages) if (image.name == "战局顶栏" || image.name == "旅程顶栏") {
                var motif = Decoration("资源绳纹 · 题签", image.transform);
                var rect = motif.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(24, -23); rect.offsetMax = new Vector2(-24, 0);
                motif.transform.SetAsFirstSibling(); images.Add(new UISkinImageSlot { target = motif, role = UISkinImageRole.Header });
            }
            foreach (var t in root.GetComponentsInChildren<Text>(true)) {
                if (t.GetComponentInParent<GestureBoard>(true) != null) continue;
                var button = t.GetComponentInParent<Button>(true);
                if (button != null) { texts.Add(new UISkinTextSlot { target = t, role = PrimaryNames.Contains(button.name) ? UISkinTextRole.PrimaryButtonLabel : UISkinTextRole.ButtonLabel }); continue; }
                bool paper = IsOnPaper(t.transform, root.transform);
                if (paper) {
                    bool title = t.name.Contains("标题") || t.name.Contains("作品名") || t.name.Contains("卷次") || t.name.Contains("题签") || t.name == "说话人";
                    texts.Add(new UISkinTextSlot { target = t, role = title ? UISkinTextRole.TitleInk : UISkinTextRole.Ink });
                }
            }
            var tracker = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "当前任务追踪");
            if (tracker != null) {
                var icon = Decoration(QuestName, tracker); var rect = icon.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
                rect.sizeDelta = new Vector2(29, 29); rect.anchoredPosition = new Vector2(-14, -13);
                images.Add(new UISkinImageSlot { target = icon, role = UISkinImageRole.QuestIcon });
            }
            // A decorative border leaves board hit testing, target nodes and all scoring graphics intact.
            foreach (var board in root.GetComponentsInChildren<GestureBoard>(true)) AddFrame(board.transform, images);
            binding.images = images.ToArray(); binding.texts = texts.ToArray();
        }

        static bool IsOnPaper(Transform target, Transform root)
        {
            for (var current = target.parent; current != null; current = current.parent) {
                if (DarkNames.Contains(current.name)) return false;
                if (PaperNames.Contains(current.name)) return true;
                if (current == root) break;
            }
            return false;
        }

        static void AddFrame(Transform parent, List<UISkinImageSlot> slots)
        {
            var frame = Decoration(FrameName, parent); var rect = frame.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            frame.transform.SetAsFirstSibling(); slots.Add(new UISkinImageSlot { target = frame, role = UISkinImageRole.Frame });
        }
        static Image Decoration(string name, Transform parent)
        {
            var existing = parent.Find(name);
            var image = existing == null ? new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>() : existing.GetComponent<Image>();
            image.transform.SetParent(parent, false); image.raycastTarget = false;
            var layout = image.GetComponent<LayoutElement>(); if (layout == null) layout = image.gameObject.AddComponent<LayoutElement>(); layout.ignoreLayout = true;
            return image;
        }
        static void Mark(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
        static string CallbackSignature(GameObject root)
        {
            var entries = new List<string>();
            foreach (var button in root.GetComponentsInChildren<Button>(true)) for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) {
                var target = button.onClick.GetPersistentTarget(i);
                entries.Add(button.GetInstanceID() + ":" + i + ":" + (target == null ? "null" : target.GetInstanceID().ToString()) + ":" + button.onClick.GetPersistentMethodName(i));
            }
            return string.Join("|", entries.ToArray());
        }
    }

    [CustomEditor(typeof(UISkinBinding))]
    sealed class UISkinBindingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("只管理这些显式外观引用。更换皮肤后点击应用；不会重建层级或覆盖玩法刷新中的状态颜色。保存场景或 Prefab 后持久化。", MessageType.Info);
            if (GUILayout.Button("应用外观（保留功能和布局）")) {
                var binding = (UISkinBinding)target;
                var changes = new List<UnityEngine.Object> { binding };
                foreach (var slot in binding.images) if (slot != null && slot.target != null) {
                    changes.Add(slot.target); var button = slot.target.GetComponent<Button>(); if (button != null) changes.Add(button);
                }
                foreach (var slot in binding.texts) if (slot != null && slot.target != null) changes.Add(slot.target);
                Undo.RecordObjects(changes.ToArray(), "Apply UI skin"); binding.Apply();
                foreach (var value in changes) { EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
                if (binding.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);
            }
        }
    }

    [CustomEditor(typeof(UISkinProfile))]
    sealed class UISkinProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (string error in ((UISkinProfile)target).ValidateConfiguration()) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("源文件与精选副本对应表：Docs/Evidence/StageD-ui-source-manifest.json。修改后用阶段 D 菜单应用到保存的界面，角色与战斗规则不受影响。", MessageType.Info);
        }
    }
}
