using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Yibi.Editor
{
    /// <summary>Creates an isolated, saved art review scene. Never changes build scenes or production actors.</summary>
    public static class ResourceReviewSetup
    {
        const string Candidates = "Assets/_Game/Art/Selected/Candidates";
        const string GuardFolder = Candidates + "/ImperialGuard";
        const string InnFolder = Candidates + "/InnKeeper";
        const string ReviewFolder = Candidates + "/ReviewEnvironment";
        const string ReviewScene = "Assets/_Game/Scenes/ResourceReview.unity";
        const string InnSource = "Assets/_Game/资源/NPC_Inn0xiannv/";

        [MenuItem("一笔江湖/3.0/保存候选角色验收场景")]
        public static void Build()
        {
            PolishEnvironmentSetup.Guard();
            string project = Directory.GetParent(Application.dataPath).FullName;
            if (!string.Equals(new DirectoryInfo(project).Name, "一笔江湖", StringComparison.Ordinal))
                throw new InvalidOperationException("候选资源仅允许在独立的一笔江湖工程准备。");
            string workspace = Directory.GetParent(project).FullName;
            string guardSource = Path.Combine(workspace, "SourceArtArchive/Imported-20260927/GuardCandidate");

            // Explicit paths only: do not scan the 80,000-file source library.
            EnsureFolder(GuardFolder); EnsureFolder(InnFolder); EnsureFolder(ReviewFolder);
            foreach (string file in new[] { "rr.fbx", "zou.fbx", "shiD.png", "shiN.png" })
                CopyCandidate(Path.Combine(guardSource, file), GuardFolder + "/" + file, workspace);
            foreach (string file in new[] { "zou.fbx", "T_NPC_Inn01_D.png" })
                CopyCandidate(Path.Combine(project, InnSource + file), InnFolder + "/" + file, workspace);

            ConfigureModel(GuardFolder + "/rr.fbx");
            ConfigureModel(GuardFolder + "/zou.fbx");
            ConfigureModel(InnFolder + "/zou.fbx");
            ConfigureTexture(GuardFolder + "/shiD.png", false);
            ConfigureTexture(GuardFolder + "/shiN.png", true);
            ConfigureTexture(InnFolder + "/T_NPC_Inn01_D.png", false);

            Material guardMaterial = CharacterMaterial(GuardFolder + "/ImperialGuard_URP.mat",
                GuardFolder + "/shiD.png", GuardFolder + "/shiN.png");
            Material innMaterial = CharacterMaterial(InnFolder + "/InnKeeper_URP.mat",
                InnFolder + "/T_NPC_Inn01_D.png", null);
            AnimationClip guardClip = WalkingClip(GuardFolder + "/zou.fbx");
            AnimationClip innClip = WalkingClip(InnFolder + "/zou.fbx");
            AnimatorController guardController = Controller(GuardFolder + "/ImperialGuard_Walk.controller", guardClip);
            AnimatorController innController = Controller(InnFolder + "/InnKeeper_Walk.controller", innClip);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environment = new GameObject("验收环境 · 不属于正式关卡");
            var actors = new GameObject("候选角色 · 独立原生蒙皮");
            var labels = new GameObject("世界说明 · 待人工验收");
            var camera = new GameObject("Review Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 2.8f, -8.3f);
            camera.transform.LookAt(new Vector3(0, 1.25f, 0));
            camera.fieldOfView = 43; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.13f, .19f, .24f);

            Material ground = SolidMaterial("ReviewGround", new Color(.23f, .29f, .28f), .2f);
            Material pedestal = SolidMaterial("ReviewPedestal", new Color(.12f, .17f, .18f), .3f);
            Material trim = SolidMaterial("ReviewBrass", new Color(.56f, .43f, .22f), .45f);
            Shape("地面", PrimitiveType.Cube, environment.transform, new Vector3(0, -.10f, 0), new Vector3(12, .2f, 10), ground);
            Shape("背景低墙", PrimitiveType.Cube, environment.transform, new Vector3(0, .45f, 2.1f), new Vector3(8, .9f, .3f), pedestal);
            foreach (float x in new[] { -1.65f, 1.65f })
            {
                Shape("角色台座", PrimitiveType.Cylinder, environment.transform, new Vector3(x, .075f, 0), new Vector3(2.3f, .075f, 2.3f), pedestal);
                Shape("台座细边", PrimitiveType.Cylinder, environment.transform, new Vector3(x, .018f, 0), new Vector3(2.35f, .012f, 2.35f), trim);
            }
            Light sun = new GameObject("主光 · 暖白", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(environment.transform); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0); sun.color = new Color(1, .91f, .79f);
            sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
            Light fill = new GameObject("补光 · 冷白", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(environment.transform); fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(20, 135, 0); fill.color = new Color(.72f, .83f, 1);
            fill.intensity = .42f; fill.shadows = LightShadows.None;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .53f, .62f);
            RenderSettings.ambientEquatorColor = new Color(.29f, .34f, .34f);
            RenderSettings.ambientGroundColor = new Color(.18f, .20f, .19f);
            RenderSettings.fog = false;
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Selected/归云晴空.mat");
            if (RenderSettings.skybox == null) RenderSettings.skybox = ReviewSky();

            GameObject guard = SaveActor("ImperialGuard_Review", GuardFolder, guardMaterial, guardController, guardClip, 1.9f);
            GameObject inn = SaveActor("InnKeeper_Review", InnFolder, innMaterial, innController, innClip, 1.85f);
            Place(guard, actors.transform, new Vector3(-1.65f, .15f, 0));
            Place(inn, actors.transform, new Vector3(1.65f, .15f, 0));
            // Built-in Latin font keeps labels serialized and portable; no transient OS Font or runtime helper.
            WorldLabel("验收标题", "CHARACTER REVIEW", labels.transform, new Vector3(0, 3.0f, .3f), .065f, new Color(.91f, .78f, .52f), camera);
            WorldLabel("守卫说明", "IMPERIAL GUARD\nOriginal rig + walking clip", labels.transform, new Vector3(-1.65f, .40f, -1.35f), .026f, Color.white, camera);
            WorldLabel("旅店人物说明", "INN KEEPER\nOwn animated mesh + clip", labels.transform, new Vector3(1.65f, .40f, -1.35f), .026f, Color.white, camera);
            WorldLabel("待验收提示", "PLAY: CHECK WALK / FEET / MATERIALS\nCandidate scene only - production actors unchanged", labels.transform,
                new Vector3(0, -.02f, -2.3f), .026f, new Color(.78f, .83f, .82f), camera);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ReviewScene)) throw new IOException("候选验收场景保存失败。");
            Selection.activeGameObject = actors;
            Debug.Log("ResourceReview saved: two candidate prefabs, own Generic rigs, looping Animator controllers. Visual and Play validation are still required. Build Settings unchanged.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        static void CopyCandidate(string source, string destination, string workspace)
        {
            source = Path.GetFullPath(source);
            string target = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, destination));
            string prefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("候选复制超出当前工作区。");
            if (!File.Exists(source)) throw new FileNotFoundException("缺少明确的候选源文件。", source);
            if (File.Exists(target))
            {
                using (var sha = SHA256.Create())
                using (var a = File.OpenRead(source))
                using (var b = File.OpenRead(target))
                    if (!sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b)))
                        throw new IOException("候选文件已修改，拒绝覆盖：" + destination);
            }
            else File.Copy(source, target, false);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }

        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("FBX 未正确导入：" + path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.optimizeGameObjects = false; importer.isReadable = false;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = true; clip.loopPose = true;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
            }
            if (clips.Length > 0) { importer.clipAnimations = clips; importer.SaveAndReimport(); }
        }

        static void ConfigureTexture(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("贴图未正确导入：" + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.maxTextureSize = 1024; importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = TextureWrapMode.Repeat; importer.SaveAndReimport();
        }

        static Shader LitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("未找到当前项目的 URP/Lit Shader。");
            return shader;
        }

        static Material CharacterMaterial(string path, string diffuse, string normal)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(LitShader()); AssetDatabase.CreateAsset(material, path); }
            material.shader = LitShader(); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffuse));
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .22f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static AnimationClip WalkingClip(string path)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && c.length > .1f).ToArray();
            if (clips.Length == 0) throw new InvalidOperationException("候选 FBX 没有可播放的动画：" + path);
            return clips.OrderByDescending(c => c.length).First();
        }

        static AnimatorController Controller(string path, AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Source Walk Loop");
            if (state == null) state = machine.AddState("Source Walk Loop");
            state.motion = clip; state.speed = 1; machine.defaultState = state;
            EditorUtility.SetDirty(state); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
            return controller;
        }

        static GameObject SaveActor(string name, string folder, Material material, AnimatorController controller, AnimationClip clip, float height)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/zou.fbx");
            if (asset == null) throw new InvalidOperationException("缺少候选动作模型：" + folder);
            var root = new GameObject(name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                model.transform.SetParent(root.transform, false); model.name = "OriginalAnimatedMesh";
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0 || skins.All(s => s.bones.Length == 0)) throw new InvalidOperationException("候选没有有效原生蒙皮：" + folder);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(folder + "/zou.fbx").OfType<Avatar>().FirstOrDefault();
                if (animator.avatar == null || !animator.avatar.isValid) throw new InvalidOperationException("候选自身的 Generic Avatar 无效：" + folder);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                clip.SampleAnimation(model, 0);
                Bounds bounds = RendererBounds(model);
                if (bounds.size.y < .01f) throw new InvalidOperationException("候选模型高度异常：" + folder);
                model.transform.localScale *= height / bounds.size.y;
                bounds = RendererBounds(model);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                // All pose, material, Animator and scale overrides are serialized into a reusable fixed prefab.
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab");
                if (prefab == null) throw new IOException("候选 Prefab 保存失败：" + name);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Bounds RendererBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("候选缺少 Renderer。");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static void Place(GameObject prefab, Transform parent, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false); instance.transform.position = position;
        }

        static Material SolidMaterial(string name, Color color, float smoothness)
        {
            string path = ReviewFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(LitShader()); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material); return material;
        }

        static Material ReviewSky()
        {
            string path = ReviewFolder + "/ReviewSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky != null) return sky;
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) throw new InvalidOperationException("缺少预览天空 Shader。");
            sky = new Material(shader); sky.SetFloat("_AtmosphereThickness", .8f); sky.SetFloat("_Exposure", 1.1f);
            AssetDatabase.CreateAsset(sky, path); return sky;
        }

        static void Shape(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var shape = GameObject.CreatePrimitive(primitive); shape.name = name;
            shape.transform.SetParent(parent, false); shape.transform.position = position; shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material; shape.isStatic = true;
        }

        static void WorldLabel(string name, string value, Transform parent, Vector3 position, float size, Color color, Camera camera)
        {
            var text = new GameObject(name, typeof(TextMesh)).GetComponent<TextMesh>();
            text.transform.SetParent(parent, false); text.transform.position = position;
            text.transform.rotation = camera.transform.rotation;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = value; text.fontSize = 48; text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
        }
    }
}
