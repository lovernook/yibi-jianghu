using UnityEditor;
using UnityEngine;
using Yibi.Progression;

namespace Yibi.Editor
{
    [CustomEditor(typeof(QuestCatalogSO))]
    public sealed class QuestCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("任务目录决定本章内容。增删任务请修改此列表；已发布的 Stable Id 不应改名。运行时使用进入存档会话时的配置快照。", MessageType.Info);
            DrawDefaultInspector();
            var catalog=(QuestCatalogSO)target;
            var errors=catalog.Validate();
            if(errors.Length==0)EditorGUILayout.HelpBox("配置通过 · "+catalog.quests.Length+" 项任务",MessageType.Info);
            else EditorGUILayout.HelpBox(string.Join("\n",errors),MessageType.Error);
        }
    }
}
