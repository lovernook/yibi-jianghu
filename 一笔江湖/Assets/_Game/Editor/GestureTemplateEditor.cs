using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Yibi.UI;

namespace Yibi.Editor
{
    [CustomEditor(typeof(GestureTemplateSO))]
    public sealed class GestureTemplateEditor : UnityEditor.Editor
    {
        private int dragging=-1;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var data=(GestureTemplateSO)target;
            string error=data.ValidationError();
            EditorGUILayout.HelpBox(error??"配置有效。坐标原点在画板左下角，节点按数组顺序连接。",error==null?MessageType.Info:MessageType.Error);
            var rect=GUILayoutUtility.GetRect(240,240);EditorGUI.DrawRect(rect,new Color(.05f,.1f,.12f));
            if(data.nodes!=null)
            {
                Handles.BeginGUI();Handles.color=new Color(.8f,.68f,.4f);
                for(int i=0;i<data.nodes.Length;i++)
                {
                    Vector2 p=new Vector2(rect.x+data.nodes[i].x*rect.width,rect.y+(1-data.nodes[i].y)*rect.height);
                    Handles.DrawWireDisc(p,Vector3.forward,data.radius*rect.width);
                    GUI.Label(new Rect(p.x-6,p.y-9,40,25),(i+1).ToString());
                    if(i>0){var prev=data.nodes[i-1];Handles.DrawLine(new Vector2(rect.x+prev.x*rect.width,rect.y+(1-prev.y)*rect.height),p);}
                }
                Handles.EndGUI();
                var e=Event.current;
                if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition))
                    for(int i=0;i<data.nodes.Length;i++){
                        var p=new Vector2(rect.x+data.nodes[i].x*rect.width,rect.y+(1-data.nodes[i].y)*rect.height);
                        if(Vector2.Distance(p,e.mousePosition)<Mathf.Max(12,data.radius*rect.width)){
                            dragging=i;Undo.RecordObject(data,"拖动穴位");GUIUtility.hotControl=GUIUtility.GetControlID(FocusType.Passive);e.Use();break;
                        }
                    }
                if(e.type==EventType.MouseDrag&&dragging>=0){
                    data.nodes[dragging]=new Vector2(Mathf.Clamp((e.mousePosition.x-rect.x)/rect.width,data.radius,1-data.radius),Mathf.Clamp(1-(e.mousePosition.y-rect.y)/rect.height,data.radius,1-data.radius));
                    EditorUtility.SetDirty(data);Repaint();e.Use();
                }
                if(e.type==EventType.MouseUp&&dragging>=0){dragging=-1;GUIUtility.hotControl=0;e.Use();}
            }
            using(new EditorGUI.DisabledScope(error!=null))
                if(GUILayout.Button("保存路线并更新场景预览"))
                {
                    EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
                    foreach(var board in Object.FindObjectsOfType<GestureBoard>())
                        if(board.template==data)
                        {
                            Undo.RecordObjects(board.GetComponentsInChildren<RectTransform>(true),"Preview route");
                            Undo.RecordObject(board.targetLine,"Preview target line");board.PreviewTemplate();
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
                        }
                }
        }
    }

    public sealed class GestureTemplateSaveGuard : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var accepted=new List<string>();
            foreach(string path in paths)
            {
                var template=AssetDatabase.LoadAssetAtPath<GestureTemplateSO>(path);
                string error=template==null?null:template.ValidationError();
                if(error==null)accepted.Add(path);
                else Debug.LogError("拒绝保存非法穴位路线："+path+" / "+error,template);
            }
            return accepted.ToArray();
        }
    }
}
