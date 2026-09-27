using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Yibi.Editor
{
    public static class M0Environment
    {
        public const string ScenePath = "Assets/_Game/Scenes/Bootstrap.unity";
        private static string Workspace => Directory.GetParent(Application.dataPath).Parent.FullName;

        [MenuItem("一笔江湖/M0/创建环境验证场景（仅首次）")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before editing assets.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your scene changes first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists; edit it in Unity.");
            Directory.CreateDirectory("Assets/_Game/Scenes");
            Directory.CreateDirectory("Assets/_Game/Data/Rendering");
            Directory.CreateDirectory("Assets/_Game/Prefabs");
            AssetDatabase.Refresh();
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/_Game/Data/Rendering/YibiRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, "Assets/_Game/Data/Rendering/YibiURP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = new GameObject("EnvironmentProbe", typeof(EnvironmentProbe));
            Undo.RegisterCreatedObjectUndo(root, "Create M0 probe");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Prefabs/EnvironmentProbe.prefab");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "一笔江湖";
            PlayerSettings.companyName = "YibiJianghu";
            AssetDatabase.SaveAssets();
            Debug.Log("YIBI_M0_SCENE_SAVED " + ScenePath);
        }

        [MenuItem("一笔江湖/M0/构建 Windows 验证包")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.Combine(Workspace, "Builds/M0"));
            Directory.CreateDirectory(Path.Combine(Workspace, "Docs/Evidence"));
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath },
                Path.Combine(Workspace, "Builds/M0/YibiJianghu.exe"),
                BuildTarget.StandaloneWindows64, BuildOptions.Development);
            string summary = "result=" + report.summary.result + "\nerrors=" + report.summary.totalErrors
                + "\nwarnings=" + report.summary.totalWarnings + "\nbytes=" + report.summary.totalSize;
            File.WriteAllText(Path.Combine(Workspace, "Docs/Evidence/M0-build.txt"), summary);
            Debug.Log("YIBI_M0_BUILD " + summary);
        }
    }
}
