using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Battle;
using Yibi.Presentation;
using Yibi.UI;

namespace Yibi.Editor
{
    /// <summary>Explicit small selection, saved Unity assets, and additive authoring. Never rebuilds source rigs or clips.</summary>
    public static class StageCArtSetup
    {
        public const string Folder = "Assets/_Game/Art/Selected/StageC";
        public const string MotionProfilePath = "Assets/_Game/Content/Presentation/GuiyunCharacterMotion.asset";
        public const string AttackPrefabPath = Folder + "/Effects/AttackSequence.prefab";
        public const string ShieldSpritePath = Folder + "/UI/Shield.png";
        public const string BurnSpritePath = Folder + "/UI/Burn.png";
        const string WorkshopPath = "Assets/_Game/Scenes/AnimationWorkshop.unity";
        const string CandidatePrefab = "Assets/_Game/Art/Selected/Candidates/ImperialGuard/ImperialGuard_Review.prefab";

        [Serializable] sealed class SourceEntry { public string source, selected, sourceSha256, selectedSha256; }
        [Serializable] sealed class SourceManifest { public string note; public List<SourceEntry> files = new List<SourceEntry>(); }

        [MenuItem("一笔江湖/3.0/C · 保存动作配置与精选美术")]
        public static void Install()
        {
            PolishEnvironmentSetup.Guard();
            if (new DirectoryInfo(Directory.GetParent(Application.dataPath).FullName).Name != "一笔江湖")
                throw new InvalidOperationException("动作配置仅允许在独立的一笔江湖工程制作。");
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EnsureFolder(Folder + "/UI"); EnsureFolder(Folder + "/Effects");
            EnsureFolder("Assets/_Game/Content/Presentation");
            var profile = AssetDatabase.LoadAssetAtPath<CharacterMotionProfile>(MotionProfilePath);
            if (profile == null) {
                profile = ScriptableObject.CreateInstance<CharacterMotionProfile>();
                AssetDatabase.CreateAsset(profile, MotionProfilePath);
            }
            if (profile.ValidateConfiguration().Length != 0)
                throw new InvalidOperationException("请先修正现有动作配置；制作工具不会覆盖手工配置。");

            PrepareSelectedArt();
            BindSavedActors(profile);
            try {
                foreach (string name in new[] {"MainMenu", "Valley", "Arena_Stone", "Arena_Bamboo", "GestureLab", "AnimationWorkshop"}) {
                    string path = "Assets/_Game/Scenes/" + name + ".unity";
                    if (!File.Exists(path)) continue;
                    var scene = EditorSceneManager.OpenScene(path);
                    bool changed = false;
                    foreach (var motion in UnityEngine.Object.FindObjectsOfType<CharacterMotion>(true))
                        if (motion.profile == null) { motion.profile = profile; EditorUtility.SetDirty(motion); changed = true; }
                    if (path == WorkshopPath) changed |= PrepareWorkshop();
                    if (changed) {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("动作场景保存失败：" + path);
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original); }
            Selection.activeObject = profile;
            Debug.Log("Stage C art saved: editable motion profile, selected 8-frame attack prefab, shield/burn sprites, native walk-only guard candidate in AnimationWorkshop. Production actors retained.");
        }

        static void BindSavedActors(CharacterMotionProfile profile)
        {
            foreach (string name in new[] {"侠客_劲装", "行者_青衫", "村中长者", "侠女_素衣", "行脚客", "布衣药师", "白发隐士", "谷中书生"}) {
                string path = "Assets/_Game/Art/Selected/Rigged/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try {
                    bool changed = false;
                    foreach (var motion in contents.GetComponentsInChildren<CharacterMotion>(true))
                        if (motion.profile == null) { motion.profile = profile; EditorUtility.SetDirty(motion); changed = true; }
                    if (changed && PrefabUtility.SaveAsPrefabAsset(contents, path) == null) throw new IOException("动作 Prefab 保存失败：" + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }

        static void PrepareSelectedArt()
        {
            var manifest = new SourceManifest {note = "用户提供的本地购买资源。只精选 8 帧和 2 图标；不发布源文件。哈希不同的既有精选文件保留手工改动。未引入 Spine Runtime。"};
            var frames = new List<Texture2D>();
            // Keep the fade-out tail; taking only the first eight bright frames would pop off.
            foreach (int i in new[] {9, 11, 13, 15, 17, 19, 21, 22}) {
                string name = "daoguang111_" + i.ToString("0000") + ".png";
                string source = "Assets/_Game/资源/C1738/1/lrsc1/images/" + name;
                string destination = Folder + "/Effects/" + name;
                bool created = CopySelection(source, destination, manifest);
                if (created) ConfigureTexture(destination, false);
                frames.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(destination));
            }
            string ui = "Assets/_Game/资源/C1869/UI/UI切图/";
            if (CopySelection(ui + "Icon_Equip_Shield.png", ShieldSpritePath, manifest)) ConfigureTexture(ShieldSpritePath, true);
            if (CopySelection(ui + "Icon_Fire.png", BurnSpritePath, manifest)) ConfigureTexture(BurnSpritePath, true);
            string manifestPath = Folder + "/SourceManifest.json";
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(AttackPrefabPath) != null) return;
            Shader shader = Shader.Find("Yibi/SequenceAdditive");
            if (shader == null || frames.Any(f => f == null)) throw new InvalidOperationException("缺少序列 Shader 或已精选帧。");
            string materialPath = Folder + "/Effects/AttackSequence.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) {
                material = new Material(shader);
                material.SetColor("_BaseColor", new Color(.58f, .87f, 1, 1));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try {
                root.name = "AttackSequence · 精选八帧";
                UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
                root.transform.localScale = Vector3.one * 2.4f;
                root.transform.localPosition = new Vector3(0, 1.15f, 0);
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material; renderer.enabled = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                var sequence = root.AddComponent<SequenceEffect>(); sequence.frames = frames.ToArray(); sequence.framesPerSecond = 16;
                if (PrefabUtility.SaveAsPrefabAsset(root, AttackPrefabPath) == null) throw new IOException("刀光 Prefab 保存失败。");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static bool PrepareWorkshop()
        {
            var workshop = UnityEngine.Object.FindObjectOfType<AnimationWorkshop>();
            if (workshop == null) throw new InvalidOperationException("动作工作台缺少保存的 AnimationWorkshop 组件。");
            bool changed = false;
            const string candidateRoot = "StageC · 原生守卫候选（仅行走）";
            if (GameObject.Find(candidateRoot) == null) {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefab);
                if (source == null) throw new InvalidOperationException("请先准备阶段 A 原生守卫候选。");
                var candidate = (GameObject)PrefabUtility.InstantiatePrefab(source);
                candidate.name = candidateRoot;
                candidate.transform.position = new Vector3(7, 0, 0);
                var camera = Camera.main;
                if (camera != null) {
                    camera.transform.position = new Vector3(1.2f, 3.4f, -15);
                    camera.transform.LookAt(new Vector3(1.2f, 1.1f, 0));
                    camera.fieldOfView = 40;
                    var label = new GameObject("原生动作能力说明", typeof(TextMesh)).GetComponent<TextMesh>();
                    label.transform.SetParent(candidate.transform, false); label.transform.localPosition = new Vector3(0, 2.55f, 0);
                    label.transform.rotation = camera.transform.rotation;
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
                    label.fontSize = 48; label.characterSize = .034f;
                    label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                    label.color = new Color(.92f, .82f, .57f); label.text = "NATIVE RIG\nWALK ONLY · NO ATTACK / HIT";
                }
                // The candidate is intentionally not in workshop.actors: its own rig has only Walk.
                workshop.candidateNote = "右侧守卫：原生骨骼与原配行走；尚无出招 / 受击，不参与左侧八动作预览";
                if (workshop.state != null) { workshop.state.text = "角色动作工作台 · 左侧为现有基础绑定，右侧为原生行走候选\n" + workshop.candidateNote; workshop.state.fontSize = 21; }
                EditorUtility.SetDirty(workshop); changed = true;
            }
            var canvas = UnityEngine.Object.FindObjectsOfType<Canvas>().FirstOrDefault(c => c.GetComponentInChildren<Button>(true) != null);
            if(canvas!=null && canvas.transform.Find("工作台说明底板")==null){
                var backdrop=ArenaSetup.Panel("工作台说明底板",canvas.transform,new Vector2(0,280),new Vector2(1220,104),new Color(.025f,.060f,.073f,1));
                backdrop.raycastTarget=false;backdrop.transform.SetAsFirstSibling();changed=true;
            }
            var native=GameObject.Find(candidateRoot);
            if(native!=null){var label=native.GetComponentInChildren<TextMesh>();if(label!=null){label.color=new Color(.04f,.09f,.12f);EditorUtility.SetDirty(label);changed=true;}}
            if (canvas != null && canvas.GetComponent<SavedChineseFont>() == null) {
                var font = canvas.gameObject.AddComponent<SavedChineseFont>();
                font.profile = AssetDatabase.LoadAssetAtPath<SystemFontProfile>("Assets/_Game/Content/UI/GuiyunSystemFontProfile.asset");
                font.Apply(); changed = true;
            }
            return changed;
        }

        static bool CopySelection(string source, string destination, SourceManifest manifest)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("精选源文件不存在。", source);
            bool created = !File.Exists(destination);
            if (created) File.Copy(source, destination, false);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            manifest.files.Add(new SourceEntry {source = source, selected = destination, sourceSha256 = Hash(source), selectedSha256 = Hash(destination)});
            return created;
        }

        static string Hash(string path)
        {
            using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static void ConfigureTexture(string path, bool sprite)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("精选图片导入失败：" + path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = sprite;
            importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.maxTextureSize = sprite ? 256 : 1024;
            importer.textureCompression = sprite ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int index = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, index));
            AssetDatabase.CreateFolder(path.Substring(0, index), path.Substring(index + 1));
        }

        public static string[] ValidateMotionAsset(CharacterMotion motion)
        {
            var errors = new List<string>();
            if (motion.profile != null) errors.AddRange(motion.profile.ValidateConfiguration());
            var animator = motion.animator != null ? motion.animator : motion.GetComponent<Animator>();
            RuntimeAnimatorController runtime = animator == null ? null : animator.runtimeAnimatorController;
            while (runtime is AnimatorOverrideController) runtime = ((AnimatorOverrideController)runtime).runtimeAnimatorController;
            var controller = runtime as AnimatorController;
            if (controller == null || controller.layers.Length == 0) { errors.Add("缺少可校验的 Animator Controller。"); return errors.ToArray(); }
            var states = new HashSet<string>(StringComparer.Ordinal);
            CollectStates(controller.layers[0].stateMachine, controller.layers[0].name + ".", states);
            string locomotion = motion.profile == null ? "Locomotion" : motion.profile.locomotionState;
            if (!states.Contains(locomotion)) errors.Add("缺少移动状态：" + locomotion);
            var actions = motion.profile == null ? CharacterMotionProfile.CreateDefaultActions() : motion.profile.actions;
            if (actions != null) foreach (var action in actions) if (action != null && !states.Contains(action.stateName)) errors.Add("缺少动作状态：" + action.stateName);
            string speed = motion.profile == null ? "Speed" : motion.profile.speedParameter;
            if (!string.IsNullOrEmpty(speed) && !controller.parameters.Any(p => p.name == speed && p.type == AnimatorControllerParameterType.Float)) errors.Add("缺少 Float 速度参数：" + speed);
            return errors.ToArray();
        }

        static void CollectStates(AnimatorStateMachine machine, string prefix, HashSet<string> states)
        {
            foreach (var item in machine.states) { states.Add(item.state.name); states.Add(prefix + item.state.name); }
            foreach (var child in machine.stateMachines) CollectStates(child.stateMachine, prefix + child.stateMachine.name + ".", states);
        }
    }

    [CustomEditor(typeof(CharacterMotionProfile))]
    sealed class CharacterMotionProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (string error in ((CharacterMotionProfile)target).ValidateConfiguration()) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("只定义表现状态与持续时间；更换角色 Controller 时还需在 CharacterMotion 上检查状态映射。动画不决定伤害。", MessageType.Info);
        }
    }

    [CustomEditor(typeof(CharacterMotion))]
    sealed class CharacterMotionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (string error in StageCArtSetup.ValidateMotionAsset((CharacterMotion)target)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}
