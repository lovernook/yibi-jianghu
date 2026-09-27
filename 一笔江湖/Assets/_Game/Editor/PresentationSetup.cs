using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using Yibi.Battle;

namespace Yibi.Editor
{
    public static class PresentationSetup
    {
        [MenuItem("一笔江湖/表现/接入运功序列与对局导航")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save and exit Play first");
            string dir="Assets/_Game/Art/620套UI特效边框BUFF光效png序列界面流光按钮帧手游技能素材/UI特效2 (115套)/打坐";
            var paths=Directory.GetFiles(dir,"*.png");System.Array.Sort(paths,System.StringComparer.Ordinal);var frames=new Texture2D[paths.Length];for(int i=0;i<paths.Length;i++)frames[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i].Replace('\\','/'));
            string materialPath=ArtPreparation.Output+"/运功序列.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(material==null){material=new Material(Shader.Find("Yibi/SequenceAdditive"));AssetDatabase.CreateAsset(material,materialPath);}
            var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            foreach(string sceneName in new[]{"Arena_Stone","Arena_Bamboo"}){
                var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+sceneName+".unity");var p=Object.FindObjectOfType<BattlePresenter>();
                foreach(var actor in new[]{p.leftActor,p.rightActor}){
                    if(actor.GetComponentInChildren<SequenceEffect>()!=null)continue;
                    var fx=GameObject.CreatePrimitive(PrimitiveType.Quad);fx.name="运功光效_序列帧";Object.DestroyImmediate(fx.GetComponent<Collider>());fx.transform.SetParent(actor,false);fx.transform.localPosition=new Vector3(0,1,0);fx.transform.localScale=Vector3.one*6;fx.GetComponent<MeshRenderer>().sharedMaterial=material;fx.AddComponent<SequenceEffect>().frames=frames;fx.GetComponent<MeshRenderer>().enabled=false;
                }
                if(p.transform.Find("离开擂台")==null){var leave=ArenaSetup.Button("离开擂台",p.transform,"离开擂台",new Vector2(-520,150),new Vector2(180,40));leave.GetComponentInChildren<Text>().font=font;UnityEventTools.AddPersistentListener(leave.onClick,p.ReturnToPractice);var resume=ArenaSetup.Button("恢复连接",p.transform,"恢复连接",new Vector2(-320,150),new Vector2(180,40));resume.GetComponentInChildren<Text>().font=font;UnityEventTools.AddPersistentListener(resume.onClick,p.ResumeConnection);}
                if(sceneName=="Arena_Bamboo"&&GameObject.Find("竹林环景")==null){
                    var group=new GameObject("竹林环景");var green=ArenaSetup.Material("竹擂台竹竿",new Color(.25f,.4f,.19f));for(int i=0;i<26;i++){float x=-13+(i%13)*2;float z=7+(i/13)*3;ArenaSetup.Shape("竹竿_"+i,PrimitiveType.Cylinder,group.transform,new Vector3(x,3,z),new Vector3(.2f,3,.2f),green);}
                    var far=GameObject.Find("远景_皇城建筑");if(far!=null)far.SetActive(false);
                    foreach(var t in p.GetComponentsInChildren<Text>(true))if(t.name=="标题")t.text="一 笔 江 湖\n听风擂台";
                }
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");AssetDatabase.SaveAssets();
        }
    }
}
