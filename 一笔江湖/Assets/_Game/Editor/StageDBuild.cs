using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Yibi.UI;
using Yibi.Presentation;

namespace Yibi.Editor
{
    public static class StageDBuild
    {
        [MenuItem("一笔江湖/3.0/D · 构建人物与界面验证版")]
        public static void Build()
        {
            PolishEnvironmentSetup.Guard();
            var skin = AssetDatabase.LoadAssetAtPath<UISkinProfile>(StageDUiSetup.ProfilePath);
            if (skin == null || skin.ValidateConfiguration().Length != 0)
                throw new InvalidOperationException("先保存并检查阶段 D 界面资源配置。");
            foreach (string id in StageDCharacterSetup.Ids)
            {
                var actor = AssetDatabase.LoadAssetAtPath<GameObject>(StageDCharacterSetup.PrefabPath(id));
                var motion = actor == null ? null : actor.GetComponentInChildren<CharacterMotion>(true);
                if (motion == null || motion.profile == null || motion.profile.ValidateConfiguration().Length != 0)
                    throw new InvalidOperationException("人物配置未完成：" + id);
            }
            ContentExporter.Export();
            string workspace = Directory.GetParent(Application.dataPath).Parent.FullName;
            string output = Path.Combine(workspace, "Builds/FrameworkStageD");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, Path.Combine(output, "YibiJianghu.exe"),
                BuildTarget.StandaloneWindows64, BuildOptions.Development);
            File.WriteAllText(Path.Combine(workspace, "Docs/Evidence/FrameworkStageD-build.txt"),
                "utc=" + DateTime.UtcNow.ToString("o") + "\nresult=" + report.summary.result +
                "\nerrors=" + report.summary.totalErrors + "\nwarnings=" + report.summary.totalWarnings +
                "\nbytes=" + report.summary.totalSize + "\nseconds=" + report.summary.totalTime.TotalSeconds);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("阶段 D 构建失败，请查看报告。");
        }
    }
}
