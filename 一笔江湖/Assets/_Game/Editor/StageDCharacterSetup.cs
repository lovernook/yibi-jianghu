using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Yibi.Presentation;
using Yibi.World;
using Yibi.Battle;

namespace Yibi.Editor
{
    // Explicit selected assets only. Source archives and older prefabs remain available for rollback.
    public static class StageDCharacterSetup
    {
        public const string Root = "Assets/_Game/Art/Selected/StageD/Characters";
        public static readonly string[] Ids = { "PlayerSwordsman", "SwordMaster", "Bandit", "TrainingDummy", "Physician", "Innkeeper", "Swordswoman" };
        static readonly string[] Models = { "M_Normal_Swords_Chuwanchun_001", "M_Normal_Swords_chongxu_001", "M_Strong_Knife_Qiangdao_001", "Muren_001", "N_M_Normal_Kongshou_daidafu_001", "N_M_Normal_kongshou_kezhanlaoban_001", "N_F_Normal_kongshou_emeidashijie_001" };
        static readonly string[] Diffuse = { "M_Normal_Swords_Chuwanchun_001_D", "M_Normal_Swords_chongxu_001_D", "M_Strong_Knife_Qiangdao_001_D", "Muren_D", "N_M_Normal_kongshou_daidafu_001_D", "N_M_Normal_kongshou_kezhanlaoban_D", "N_F_Normal_kongshou_emeidashijie_001_D" };
        public static string ModelPath(int i) { return Root + "/" + Ids[i] + "/" + Models[i] + ".fbx"; }
        public static string PrefabPath(string id) { return Root + "/" + id + "/" + id + ".prefab"; }
        static string Folder(int i) { return Root + "/" + Ids[i]; }

        [MenuItem("一笔江湖/3.0/D · 导入原生人物（第一步）")]
        public static void ImportSelected()
        {
            PolishEnvironmentSetup.Guard();
            for (int i = 0; i < Ids.Length; i++)
            {
                string path = ModelPath(i);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new IOException("无法导入精选 FBX：" + path);
                importer.globalScale = 100f;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importCameras = false; importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.optimizeGameObjects = false; importer.isReadable = false;
                importer.SaveAndReimport();
                var definitions = importer.defaultClipAnimations;
                foreach (var clip in definitions)
                {
                    clip.loopTime = clip.name == "Stand" || clip.name == "Walk" || clip.name == "Run" || clip.name == "Relax";
                    clip.loopPose = clip.loopTime;
                    clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
                }
                if (definitions.Length != 0) { importer.clipAnimations = definitions; importer.SaveAndReimport(); }
                string texture = Folder(i) + "/" + Diffuse[i] + ".png";
                AssetDatabase.ImportAsset(texture, ImportAssetOptions.ForceSynchronousImport);
                var image = AssetImporter.GetAtPath(texture) as TextureImporter;
                image.textureType = TextureImporterType.Default; image.sRGBTexture = true;
                image.mipmapEnabled = true; image.isReadable = false; image.maxTextureSize = 1024;
                image.wrapMode = TextureWrapMode.Repeat; image.textureCompression = TextureImporterCompression.Compressed;
                image.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Stage D selected native FBX imported; inspect actual clips/skin before BuildPrefabs.");
        }

        [Serializable] public sealed class CharacterAudit { public string id; public string model; public bool avatarValid; public int skins; public int boundBones; public string[] clips; public float[] durations; public string error; }
        public static CharacterAudit[] InspectSelected()
        {
            var result = new List<CharacterAudit>();
            for (int i = 0; i < Ids.Length; i++)
            {
                var a = new CharacterAudit { id = Ids[i], model = ModelPath(i) };
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(a.model);
                if (asset == null) { a.error = "FBX unavailable"; result.Add(a); continue; }
                var skins = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                a.skins = skins.Length; a.boundBones = skins.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(a.model).OfType<Avatar>().FirstOrDefault();
                a.avatarValid = avatar != null && avatar.isValid;
                var clips = Clips(i); a.clips = clips.Select(c => c.name).ToArray(); a.durations = clips.Select(c => c.length).ToArray();
                result.Add(a);
            }
            return result.ToArray();
        }

        static AnimationClip[] Clips(int i) { return AssetDatabase.LoadAllAssetsAtPath(ModelPath(i)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray(); }
        static AnimationClip Clip(int i, string name)
        {
            var clip = Clips(i).FirstOrDefault(c => c.name == name);
            if (clip == null || clip.length <= 0) throw new InvalidOperationException(Ids[i] + " 缺少原配片段：" + name);
            return clip;
        }

        [MenuItem("一笔江湖/3.0/D · 保存原生人物 Prefab（第二步）")]
        public static void BuildPrefabs()
        {
            PolishEnvironmentSetup.Guard();
            for (int i = 0; i < Ids.Length; i++) BuildPrefab(i);
            AssetDatabase.SaveAssets();
        }

        static void BuildPrefab(int i)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(i));
            if (asset == null) throw new InvalidOperationException("先导入并核验精选人物。");
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath(i)).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid) throw new InvalidOperationException("原生 Avatar 无效：" + Ids[i]);
            var stand = Clip(i, "Stand"); bool combat = i < 4;
            if (combat) foreach (string n in new[] { "Walk", "Run", "Attack", "Magic", "Hit", "Die", "Relax" }) Clip(i, n);
            var controller = CreateController(i, combat);
            var profile = CreateProfile(i, combat);
            string matPath = Folder(i) + "/" + Ids[i] + "_URP.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder(i) + "/" + Diffuse[i] + ".png"));
                mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", .15f);
                mat.SetFloat("_Cull", (float)CullMode.Off); AssetDatabase.CreateAsset(mat, matPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Ids[i])) != null) return; // preserve Inspector edits on rerun
            var root = new GameObject(Ids[i] + " · 原生骨骼");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                model.transform.SetParent(root.transform, false); model.name = "NativeModel";
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var motion = model.AddComponent<CharacterMotion>(); motion.animator = animator; motion.profile = profile;
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null || skin.bones.Length == 0 || skin.bones.Any(b => b == null)) throw new InvalidOperationException("原生蒙皮绑定不完整：" + Ids[i]);
                    skin.sharedMaterials = Enumerable.Repeat(mat, Math.Max(1, skin.sharedMaterials.Length)).ToArray();
                    skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
                }
                stand.SampleAnimation(model, 0);
                Bounds bounds = BoundsOf(model);
                if (bounds.size.y < .01f) throw new InvalidOperationException("人物包围盒无效。");
                float height = i == 3 ? 2.20f : 2.40f;
                model.transform.localScale *= height / bounds.size.y;
                bounds = BoundsOf(model);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                RestoreSourcePose(model, i);
                BakeCullingBounds(model, Clips(i));
                stand.SampleAnimation(model, 0);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(Ids[i]));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AnimatorController CreateController(int i, bool combat)
        {
            string path = Folder(i) + "/" + Ids[i] + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (existing != null) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Locomotion"); machine.defaultState = idle;
            if (combat)
            {
                var blend = new BlendTree { name = "原配 Stand · Walk · Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(blend, controller);
                blend.AddChild(Clip(i, "Stand"), 0); blend.AddChild(Clip(i, "Walk"), 3.5f); blend.AddChild(Clip(i, "Run"), 6);
                idle.motion = blend;
                AddState(machine, "Attack", Clip(i, "Attack")); AddState(machine, "Cast", Clip(i, "Magic"));
                AddState(machine, "Hit", Clip(i, "Hit")); AddState(machine, "Defeat", Clip(i, "Die"));
                AddState(machine, "Victory", Clip(i, "Relax")); // source Relax is a neutral recovery, not a newly authored victory animation
            }
            else idle.motion = Clip(i, "Stand");
            EditorUtility.SetDirty(controller); return controller;
        }
        static void AddState(AnimatorStateMachine machine, string id, AnimationClip clip) { var state = machine.AddState(id); state.motion = clip; state.speed = id == "Cast" ? Mathf.Max(1, clip.length / 1.4f) : 1; state.writeDefaultValues = true; }
        static CharacterMotionProfile CreateProfile(int i, bool combat)
        {
            string path = Folder(i) + "/" + Ids[i] + "_Motion.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CharacterMotionProfile>(path); if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<CharacterMotionProfile>();
            if (combat)
            {
                profile.actions = new[] {
                    Action("Attack", Clip(i,"Attack")), Action("Cast", Clip(i,"Magic")), Action("Hit",Clip(i,"Hit")),
                    Action("Victory",Clip(i,"Relax"),true), Action("Defeat",Clip(i,"Die"),true)
                };
            }
            else profile.actions = new CharacterMotionAction[0];
            AssetDatabase.CreateAsset(profile, path); return profile;
        }
        static CharacterMotionAction Action(string id, AnimationClip clip, bool terminal = false) { return new CharacterMotionAction { id = id, stateName = id, transitionSeconds = .09f, durationSeconds = Mathf.Max(.05f, id == "Cast" ? Mathf.Min(clip.length, 1.4f) : clip.length), holdUntilReset = terminal }; }
        // FBX 6 import bounds can be a tiny placeholder. Measure actual skinned vertices, never that placeholder.
        public static Bounds BoundsOf(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("人物没有 SkinnedMeshRenderer。");
            bool initialized = false; var result = new Bounds(); var baked = new Mesh();
            try
            {
                foreach (var renderer in renderers)
                {
                    // true compensates the renderer scale; TransformPoint below applies it exactly once.
                    renderer.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = renderer.transform.TransformPoint(vertex);
                        if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; } else result.Encapsulate(point);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            if (!initialized) throw new InvalidOperationException("原生蒙皮没有可测量顶点。");
            return result;
        }
        static void BakeCullingBounds(GameObject model, AnimationClip[] clips)
        {
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                // This purchased break-apart mesh includes pieces outside the animated Spine branch.
                // A preview probe across all eight clips proved that using the stable model root changes
                // no skinned world vertex (104,840 comparisons, max delta 0), while preserving correct culling.
                if (renderer.sharedMesh != null && AssetDatabase.GetAssetPath(renderer.sharedMesh) == ModelPath(3))
                    renderer.rootBone = model.transform;
            }
            var bounds = new Bounds[renderers.Length]; var initialized = new bool[renderers.Length]; var baked = new Mesh();
            var vertices = new List<Vector3>();
            var neutralPose = CapturePose(model.transform);
            try
            {
                foreach (var clip in clips)
                {
                    // Death/break-apart clips can turn the bounds root sharply between sparse samples.
                    // Offline authoring samples twice per source frame (at least 60 Hz); no runtime baking is added.
                    int samples = Mathf.Max(1, Mathf.CeilToInt(clip.length * Mathf.Max(30f, clip.frameRate) * 2f));
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        RestorePose(neutralPose);
                        clip.SampleAnimation(model, Mathf.Min(clip.length - .0001f, clip.length * sample / samples));
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            var renderer = renderers[i];
                            renderer.BakeMesh(baked, true);
                            // A skinned renderer's serialized localBounds follows rootBone, not the mesh object's transform.
                            // BakeMesh(true) supplies renderer-local vertices; convert each sampled pose into that bounds frame.
                            Transform boundsFrame = renderer.rootBone == null ? renderer.transform : renderer.rootBone;
                            Matrix4x4 toBounds = boundsFrame.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                            baked.GetVertices(vertices);
                            foreach (var vertex in vertices)
                            {
                                Vector3 point = toBounds.MultiplyPoint3x4(vertex);
                                if (!initialized[i]) { bounds[i] = new Bounds(point, Vector3.zero); initialized[i] = true; }
                                else bounds[i].Encapsulate(point);
                            }
                        }
                    }
                }
                for (int i = 0; i < renderers.Length; i++)
                {
                    bounds[i].Expand(bounds[i].size * .25f + Vector3.one * .04f);
                    renderers[i].localBounds = bounds[i];
                    renderers[i].updateWhenOffscreen = false;
                }
            }
            finally { RestorePose(neutralPose); UnityEngine.Object.DestroyImmediate(baked); }
        }

        // Repairs culling data only. Authored transforms, motion profiles, controllers and UI are preserved.
        [MenuItem("一笔江湖/3.0/D · 修复原生人物骨骼空间剔除边界")]
        public static void RepairSavedCullingBounds()
        {
            PolishEnvironmentSetup.Guard();
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            try
            {
                for (int i = 0; i < Ids.Length; i++)
                {
                    string path = PrefabPath(Ids[i]);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var motion = root.GetComponentInChildren<CharacterMotion>(true);
                        if (motion == null) throw new InvalidOperationException("人物缺少动作组件：" + Ids[i]);
                        RepairModelCulling(motion.gameObject, i);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                string explorerPath = "Assets/_Game/Prefabs/PlayerExplorer.prefab";
                var explorerRoot = PrefabUtility.LoadPrefabContents(explorerPath);
                try
                {
                    RepairCullingForActors(explorerRoot.GetComponentsInChildren<CharacterMotion>(true));
                    PrefabUtility.SaveAsPrefabAsset(explorerRoot, explorerPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(explorerRoot); }
                AssetDatabase.SaveAssets();
                foreach (string sceneName in new[] { "MainMenu", "Valley", "Arena_Stone", "Arena_Bamboo", "GestureLab", "AnimationWorkshop" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                    int count = RepairCullingForActors(UnityEngine.Object.FindObjectsOfType<CharacterMotion>(true));
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("剔除边界保存失败：" + sceneName);
                    Debug.Log("Stage D culling bounds saved in root-bone space: " + sceneName + " / " + count);
                }
                AssetDatabase.SaveAssets();
            }
            finally { if (!string.IsNullOrEmpty(original) && File.Exists(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single); }
        }
        static int RepairCullingForActors(CharacterMotion[] actors)
        {
            int count = 0;
            foreach (var motion in actors)
            {
                if (motion.animator == null) continue;
                string path = AssetDatabase.GetAssetPath(motion.animator.runtimeAnimatorController);
                int i = Array.FindIndex(Ids, id => path == Root + "/" + id + "/" + id + ".controller");
                if (i < 0) continue;
                RepairModelCulling(motion.gameObject, i); count++;
            }
            return count;
        }
        static void RepairModelCulling(GameObject model, int i)
        {
            var authoredPose = CapturePose(model.transform);
            try { RestoreSourcePose(model, i); BakeCullingBounds(model, Clips(i)); }
            finally { RestorePose(authoredPose); }
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        [Serializable] public sealed class CullingAudit
        {
            public string id;
            public int poses, checkedVertices, outsideVertices;
            public string firstOutsideClip;
            public float firstOutsideTime;
            public bool passed;
        }
        /// <summary>Read-only preview check. Uses a different, denser sample grid than bounds authoring.</summary>
        public static CullingAudit[] ValidateSavedCullingBounds()
        {
            var results = new List<CullingAudit>();
            for (int i = 0; i < Ids.Length; i++)
            {
                var preview = EditorSceneManager.NewPreviewScene();
                var mesh = new Mesh();
                var vertices = new List<Vector3>();
                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Ids[i]));
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    var model = root.GetComponentInChildren<CharacterMotion>(true).gameObject;
                    RestoreSourcePose(model, i); var neutralPose = CapturePose(model.transform);
                    var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var result = new CullingAudit { id = Ids[i] };
                    foreach (var clip in Clips(i))
                    {
                        // Three samples per source frame plus one interval avoids merely rechecking authoring times.
                        int samples = Mathf.Max(32, Mathf.CeilToInt(clip.length * Mathf.Max(30f, clip.frameRate) * 3f) + 1);
                        for (int sample = 0; sample <= samples; sample++)
                        {
                            RestorePose(neutralPose);
                            float time = Mathf.Min(clip.length - .0001f, clip.length * sample / samples);
                            clip.SampleAnimation(model, time); result.poses++;
                            foreach (var renderer in renderers)
                            {
                                renderer.BakeMesh(mesh, true);
                                Bounds world = renderer.bounds;
                                world.Expand(.002f); // floating-point tolerance only, not a culling margin
                                mesh.GetVertices(vertices);
                                foreach (var vertex in vertices)
                                {
                                    result.checkedVertices++;
                                    if (world.Contains(renderer.transform.TransformPoint(vertex))) continue;
                                    result.outsideVertices++;
                                    if (result.firstOutsideClip == null) { result.firstOutsideClip = clip.name; result.firstOutsideTime = time; }
                                }
                            }
                        }
                    }
                    result.passed = result.outsideVertices == 0;
                    results.Add(result);
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); EditorSceneManager.ClosePreviewScene(preview); }
            }
            return results.ToArray();
        }

        sealed class TransformPose
        {
            public Transform target;
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        static TransformPose[] CapturePose(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(t => new TransformPose {
                target = t, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray();
        }
        static void RestorePose(TransformPose[] pose)
        {
            foreach (var item in pose)
            {
                if (item.target == null) continue;
                item.target.localPosition = item.position;
                item.target.localRotation = item.rotation;
                item.target.localScale = item.scale;
            }
        }
        static void RestoreSourcePose(GameObject model, int i)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(i));
            if (source == null) throw new InvalidOperationException("缺少原生骨架来源：" + Ids[i]);
            var transforms = source.GetComponentsInChildren<Transform>(true).Where(t => t != source.transform)
                .ToDictionary(t => AnimationUtility.CalculateTransformPath(t, source.transform), t => t);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue; // keep authored placement, facing and normalization on the model root
                Transform original;
                if (!transforms.TryGetValue(AnimationUtility.CalculateTransformPath(t, model.transform), out original)) continue;
                t.localPosition = original.localPosition; t.localRotation = original.localRotation; t.localScale = original.localScale;
            }
        }

        [MenuItem("一笔江湖/3.0/D · 修复已保存人物尺度和原始姿态")]
        public static void RepairSavedPrefabsAndScenes()
        {
            PolishEnvironmentSetup.Guard();
            for (int i = 0; i < Ids.Length; i++)
            {
                string path = PrefabPath(Ids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var motion = root.GetComponentInChildren<CharacterMotion>(true);
                    if (motion == null) throw new InvalidOperationException("已保存人物缺少动作组件：" + path);
                    var model = motion.gameObject;
                    model.transform.localScale = Vector3.one;
                    model.transform.localPosition = Vector3.zero;
                    RestoreSourcePose(model, i);
                    var stand = Clip(i, "Stand"); stand.SampleAnimation(model, 0);
                    float targetHeight = i == 3 ? 2.20f : 2.40f;
                    var bounds = BoundsOf(model);
                    model.transform.localScale *= targetHeight / bounds.size.y;
                    bounds = BoundsOf(model);
                    model.transform.position += root.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    // Sparse Stand clips do not reset the pieces moved only by Die. Restore source TRS before every sample.
                    RestoreSourcePose(model, i);
                    BakeCullingBounds(model, Clips(i));
                    stand.SampleAnimation(model, 0);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();

            string explorerPath = "Assets/_Game/Prefabs/PlayerExplorer.prefab";
            var explorerRoot = PrefabUtility.LoadPrefabContents(explorerPath);
            try
            {
                RepairPlacedActors(explorerRoot.GetComponentsInChildren<CharacterMotion>(true));
                var explorer = explorerRoot.GetComponent<ValleyExplorer>();
                if (explorer != null) explorer.motion = explorerRoot.GetComponentInChildren<CharacterMotion>(true);
                PrefabUtility.SaveAsPrefabAsset(explorerRoot, explorerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(explorerRoot); }

            foreach (string sceneName in new[] { "MainMenu", "Valley", "Arena_Stone", "Arena_Bamboo", "GestureLab", "AnimationWorkshop" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                int count = RepairPlacedActors(UnityEngine.Object.FindObjectsOfType<CharacterMotion>(true));
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("人物尺度修复场景保存失败：" + sceneName);
                Debug.Log("Stage D repaired source pose and measured scale: " + sceneName + " / " + count);
            }
            AssetDatabase.SaveAssets();
        }
        static int RepairPlacedActors(CharacterMotion[] actors)
        {
            int count = 0;
            foreach (var motion in actors)
            {
                if (motion.animator == null) continue;
                string controller = AssetDatabase.GetAssetPath(motion.animator.runtimeAnimatorController);
                int i = Array.FindIndex(Ids, id => controller == Root + "/" + id + "/" + id + ".controller");
                if (i < 0) continue;
                var model = motion.gameObject;
                var instanceRoot = model.transform.parent;
                if (instanceRoot == null) throw new InvalidOperationException("原生角色缺少保存的 Prefab 根。");
                var placement = instanceRoot.parent;
                Vector3 anchor = placement == null ? instanceRoot.position : placement.position;
                // The old first install stored an erroneous inverse-scale override on this outer instance.
                instanceRoot.localScale = Vector3.one;
                if (placement != null) instanceRoot.localPosition = Vector3.zero;
                RestoreSourcePose(model, i); Clip(i, "Stand").SampleAnimation(model, 0);
                var bounds = BoundsOf(model);
                float targetHeight = i == 3 ? 2.20f : 2.40f;
                instanceRoot.localScale *= targetHeight / bounds.size.y;
                bounds = BoundsOf(model);
                instanceRoot.position += anchor - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                // Persist scene/Pefab overrides, including every bone restored from stale sampled pose.
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instanceRoot);
                EditorUtility.SetDirty(instanceRoot); count++;
            }
            return count;
        }
        [MenuItem("一笔江湖/3.0/D · 替换正式场景人物（第三步）")]
        public static void ReplaceSceneActors()
        {
            PolishEnvironmentSetup.Guard();
            foreach (string sceneName in new[] { "MainMenu", "Valley", "Arena_Stone", "Arena_Bamboo", "GestureLab", "AnimationWorkshop" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
                var oldActors = UnityEngine.Object.FindObjectsOfType<CharacterMotion>(true)
                    .Where(m => m.animator != null && AssetDatabase.GetAssetPath(m.animator.runtimeAnimatorController).StartsWith("Assets/_Game/Art/Selected/Rigged/", StringComparison.Ordinal)).ToArray();
                if (oldActors.Length == 0) continue;
                var replaced = new Dictionary<CharacterMotion, CharacterMotion>();
                int index = 0;
                foreach (var old in oldActors)
                {
                    string id = SelectRole(sceneName, old, index++);
                    replaced[old] = ReplaceInPlace(old, id);
                }
                foreach (var explorer in UnityEngine.Object.FindObjectsOfType<ValleyExplorer>(true))
                {
                    if (explorer.motion != null && replaced.ContainsKey(explorer.motion)) explorer.motion = replaced[explorer.motion];
                    if (explorer.motion == null) explorer.motion = explorer.GetComponentInChildren<CharacterMotion>(true);
                    // Preserve existing visual turning pivot; inner model carries any facing correction.
                    EditorUtility.SetDirty(explorer); PrefabUtility.RecordPrefabInstancePropertyModifications(explorer);
                }
                foreach (var workshop in UnityEngine.Object.FindObjectsOfType<AnimationWorkshop>(true))
                {
                    workshop.actors = oldActors.Select(a => replaced[a]).ToArray();
                    workshop.candidateNote = "四名正式角色均使用购买资源的原生蒙皮与原配 8 个片段；胜利映射 Relax 收势。右侧守卫仍仅行走。";
                    if (workshop.previews != null && workshop.previews.Length > 3) workshop.previews[3].label = "原配攻击";
                    EditorUtility.SetDirty(workshop);
                }
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("正式人物场景保存失败：" + sceneName);
                Debug.Log("Stage D native actors saved: " + sceneName + " / " + oldActors.Length);
            }
            AssetDatabase.SaveAssets();
        }
        public static void ReplaceExplorerPrefab()
        {
            PolishEnvironmentSetup.Guard();
            string path = "Assets/_Game/Prefabs/PlayerExplorer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var explorer = root.GetComponent<ValleyExplorer>();
                var old = root.GetComponentsInChildren<CharacterMotion>(true).FirstOrDefault(m => m.animator != null && AssetDatabase.GetAssetPath(m.animator.runtimeAnimatorController).StartsWith("Assets/_Game/Art/Selected/Rigged/", StringComparison.Ordinal));
                CharacterMotion motion;
                if (old != null) motion = ReplaceInPlace(old, "PlayerSwordsman");
                else
                {
                    // Very early PlayerExplorer prefabs only had primitive/static visual renderers.
                    if (explorer == null || explorer.visual == null) throw new InvalidOperationException("PlayerExplorer 缺少保存的视觉枢轴。");
                    motion = explorer.visual.GetComponentInChildren<CharacterMotion>(true);
                    if (motion == null)
                    {
                        foreach (var renderer in explorer.visual.GetComponentsInChildren<Renderer>(true))
                        {
                            var meshFilter = renderer.GetComponent<MeshFilter>();
                            UnityEngine.Object.DestroyImmediate(renderer);
                            if (meshFilter != null) UnityEngine.Object.DestroyImmediate(meshFilter);
                        }
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("PlayerSwordsman"));
                        if (prefab == null) throw new InvalidOperationException("先保存原生玩家 Prefab。");
                        var native = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                        native.transform.SetParent(explorer.visual, false);
                        native.name = "正式人物 · PlayerSwordsman";
                        motion = native.GetComponentInChildren<CharacterMotion>(true);
                    }
                }
                if (explorer != null) explorer.motion = motion;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static string SelectRole(string scene, CharacterMotion actor, int index)
        {
            if (scene == "MainMenu" || scene == "GestureLab") return "PlayerSwordsman";
            if (scene == "AnimationWorkshop")
            {
                if (actor.name.Contains("侠客")) return "PlayerSwordsman";
                if (actor.name.Contains("行者")) return "Bandit";
                if (actor.name.Contains("长者")) return "SwordMaster";
                return "TrainingDummy";
            }
            if (scene.StartsWith("Arena", StringComparison.Ordinal))
            {
                var battle = UnityEngine.Object.FindObjectOfType<BattlePresenter>();
                if (battle != null && actor.transform.IsChildOf(battle.leftActor)) return "PlayerSwordsman";
                return scene == "Arena_Stone" ? "SwordMaster" : "Bandit";
            }
            if (actor.GetComponentInParent<ValleyExplorer>() != null) return "PlayerSwordsman";
            string path = HierarchyPath(actor.transform);
            if (path.Contains("药师")) return "Physician";
            if (path.Contains("侠女") || path.Contains("岚音")) return "Swordswoman";
            if (path.Contains("听松")) return "SwordMaster";
            if (path.Contains("游侠")) return "PlayerSwordsman";
            return "Innkeeper";
        }
        static string HierarchyPath(Transform t) { string path = t.name; while (t.parent != null) { t = t.parent; path = t.name + "/" + path; } return path; }
        static CharacterMotion ReplaceInPlace(CharacterMotion old, string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id));
            if (prefab == null) throw new InvalidOperationException("缺少已验证人物 Prefab：" + id);
            var pivot = old.transform;
            var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(pivot.gameObject);
            if (outer != null) PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var oldBounds = BoundsOf(pivot.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot.gameObject.scene);
            model.transform.SetParent(pivot, false);
            model.name = "正式人物 · " + id;
            var replacement = model.GetComponentInChildren<CharacterMotion>(true);
            // Existing actor placement defines stage footprint; native mesh supplies skin/rig/pose.
            float height = id == "TrainingDummy" ? 2.20f : 2.40f;
            var newBounds = BoundsOf(model);
            model.transform.localScale *= height / newBounds.size.y;
            newBounds = BoundsOf(model);
            float footY = pivot.position.y;
            model.transform.position += new Vector3(pivot.position.x - newBounds.center.x, footY - newBounds.min.y, pivot.position.z - newBounds.center.z);
            foreach (var explorer in UnityEngine.Object.FindObjectsOfType<ValleyExplorer>(true)) if (explorer.motion == old) { explorer.motion = replacement; EditorUtility.SetDirty(explorer); }
            foreach (var workshop in UnityEngine.Object.FindObjectsOfType<AnimationWorkshop>(true)) if (workshop.actors != null) for (int j = 0; j < workshop.actors.Length; j++) if (workshop.actors[j] == old) workshop.actors[j] = replacement;
            var oldAnimator = old.animator;
            UnityEngine.Object.DestroyImmediate(old);
            if (oldAnimator != null) UnityEngine.Object.DestroyImmediate(oldAnimator);
            // Legacy generated prefabs contain only these two visual branches. Interaction and FX siblings remain.
            foreach (string branch in new[] { "Rig", "Skin" }) { var child = pivot.Find(branch); if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject); }
            return replacement;
        }
    }
}






