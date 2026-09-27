using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Yibi.World;
using Yibi.UI;
using Yibi.Battle;

namespace Yibi.Editor
{
    public static class ValleySetup
    {
        [MenuItem("一笔江湖/世界/创建归云谷（仅首次）")]
        public static void Create()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save and exit Play first");
            if(File.Exists("Assets/_Game/Scenes/Valley.unity"))throw new InvalidOperationException("Valley exists; edit saved layout");
            // Reuse the style helpers; font is assigned to every saved label below.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var light=UnityEngine.Object.FindObjectOfType<Light>();light.transform.rotation=Quaternion.Euler(50,-30,0);light.intensity=1.3f;
            RenderSettings.ambientLight=new Color(.58f,.64f,.60f);Camera.main.farClipPlane=250;Camera.main.backgroundColor=new Color(.29f,.40f,.43f);Camera.main.clearFlags=CameraClearFlags.SolidColor;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=160;RenderSettings.fogColor=Camera.main.backgroundColor;
            var world=new GameObject("归云谷_可编辑布局");
            var grass=ArenaSetup.Material("山谷苔地",new Color(.27f,.36f,.26f));var stone=ArenaSetup.Material("山谷石径",new Color(.55f,.53f,.44f));var dark=ArenaSetup.Material("远山",new Color(.20f,.28f,.27f));var green=ArenaSetup.Material("竹叶",new Color(.13f,.30f,.20f));var bamboo=ArenaSetup.Material("竹竿",new Color(.38f,.45f,.20f));
            ArenaSetup.Shape("山谷地面",PrimitiveType.Cube,world.transform,new Vector3(0,-.5f,20),new Vector3(180,1,180),grass);
            ArenaSetup.Shape("南北石径",PrimitiveType.Cube,world.transform,new Vector3(0,.01f,20),new Vector3(8,.08f,120),stone);
            ArenaSetup.Shape("竹林支路",PrimitiveType.Cube,world.transform,new Vector3(23,.02f,0),new Vector3(46,.08f,7),stone);
            for(int i=0;i<16;i++){float angle=i*Mathf.PI*2/16;var mountain=ArenaSetup.Shape("远山_"+i,PrimitiveType.Sphere,world.transform,new Vector3(Mathf.Cos(angle)*90,8,20+Mathf.Sin(angle)*90),new Vector3(35,35+(i%3)*9,35),dark);mountain.isStatic=true;}
            for(int i=0;i<40;i++){
                float x=22+(i%8)*4,z=-20+(i/8)*10; if(Mathf.Abs(z)<5)z+=8;
                var stalk=ArenaSetup.Shape("竹_"+i,PrimitiveType.Cylinder,world.transform,new Vector3(x,3.5f,z),new Vector3(.3f,3.5f,.3f),bamboo);
                var leaf=ArenaSetup.Shape("竹冠_"+i,PrimitiveType.Sphere,stalk.transform,new Vector3(0,1,0),new Vector3(9,.45f,8),green);UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
            }
            var village=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArtPreparation.Output+"/Environment_49-50.prefab"));village.name="村落远景_选用资源";village.transform.position=new Vector3(-50,-3,0);village.transform.localScale=Vector3.one*.75f;
            var ruin=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArtPreparation.Output+"/Environment_61.prefab"));ruin.name="遗迹远景_选用资源";ruin.transform.position=new Vector3(-28,0,65);ruin.transform.localScale=Vector3.one*.5f;
            foreach(var root in new[]{village,ruin})foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.sharedMaterial.mainTexture==null)r.enabled=false;
            var player=new GameObject("PlayerExplorer",typeof(CharacterController),typeof(ValleyExplorer));player.transform.position=new Vector3(0,1,-14);var cc=player.GetComponent<CharacterController>();cc.height=2.5f;cc.center=new Vector3(0,1.3f,0);cc.radius=.45f;cc.stepOffset=.4f;
            var actor=ArenaSetup.Actor("行者外观",Vector3.zero,new Color(.12f,.38f,.34f));actor.transform.SetParent(player.transform,false);foreach(var c in actor.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            var explorer=player.GetComponent<ValleyExplorer>();explorer.visual=actor.transform;explorer.followCamera=Camera.main;Camera.main.transform.position=player.transform.position+explorer.cameraOffset;Camera.main.transform.LookAt(player.transform.position);
            var canvas=new GameObject("ValleyHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(LocalChineseFont),typeof(ValleyPresenter));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var p=canvas.GetComponent<ValleyPresenter>();p.explorer=explorer;
            var header=ArenaSetup.Panel("山谷标题栏",canvas.transform,new Vector2(0,310),new Vector2(1240,80),new Color(.06f,.12f,.12f,.9f));p.region=ArenaSetup.Label("区域",header.transform,"归云谷",new Vector2(-350,0),new Vector2(490,65),28);
            var bag=ArenaSetup.Button("行囊",header.transform,"秘籍行囊 [Tab]",new Vector2(490,0),new Vector2(210,54));UnityEventTools.AddPersistentListener(bag.onClick,p.ToggleInventory);
            p.prompt=ArenaSetup.Label("交互提示",canvas.transform,"",new Vector2(0,-255),new Vector2(1100,55),25);p.prompt.alignment=TextAnchor.MiddleCenter;
            p.notice=ArenaSetup.Label("消息",canvas.transform,"",new Vector2(0,-320),new Vector2(1200,65),18);p.notice.alignment=TextAnchor.MiddleCenter;
            p.trialText=ArenaSetup.Label("疾行进度",canvas.transform,"",new Vector2(0,235),new Vector2(800,60),24);p.trialText.alignment=TextAnchor.MiddleCenter;
            var inv=ArenaSetup.Panel("秘籍行囊",canvas.transform,Vector2.zero,new Vector2(950,440),new Color(.05f,.10f,.11f,.98f));p.inventoryPanel=inv.gameObject;
            p.profileText=ArenaSetup.Label("存档与抽签说明",inv.transform,"",new Vector2(0,105),new Vector2(880,170),21);
            p.equipButtons=new Button[3];for(int i=0;i<3;i++){p.equipButtons[i]=ArenaSetup.Button("装配槽"+i,inv.transform,"",new Vector2(-300+i*300,-30),new Vector2(275,65));UnityEventTools.AddIntPersistentListener(p.equipButtons[i].onClick,p.Equip,i);}
            var mind=ArenaSetup.Button("心法",inv.transform,"",new Vector2(-175,-120),new Vector2(510,65));p.mindsetLabel=mind.GetComponentInChildren<Text>();UnityEventTools.AddPersistentListener(mind.onClick,p.SwitchMindset);
            var draw=ArenaSetup.Button("免费抽签",inv.transform,"消耗1签令 · 抽签",new Vector2(295,-120),new Vector2(250,65));UnityEventTools.AddPersistentListener(draw.onClick,p.Draw);
            var close=ArenaSetup.Button("关闭",inv.transform,"收起行囊",new Vector2(0,-195),new Vector2(220,40));UnityEventTools.AddPersistentListener(close.onClick,p.ToggleInventory);
            var pause=ArenaSetup.Panel("菜单",canvas.transform,Vector2.zero,new Vector2(450,260),new Color(.05f,.10f,.11f,.98f));p.pausePanel=pause.gameObject;
            var resume=ArenaSetup.Button("继续",pause.transform,"继续探索",new Vector2(0,50),new Vector2(360,65));UnityEventTools.AddPersistentListener(resume.onClick,p.Resume);
            var quit=ArenaSetup.Button("退出",pause.transform,"保存并退出",new Vector2(0,-50),new Vector2(360,65));UnityEventTools.AddPersistentListener(quit.onClick,p.Quit);
            Vector3[] points={new Vector3(-6,0,-10),new Vector3(40,0,0),new Vector3(0,0,45),new Vector3(8,0,35),new Vector3(6,0,-10)};p.interactionPoints=new Transform[5];
            for(int i=0;i<5;i++){var marker=ArenaSetup.Shape(p.interactionNames[i],PrimitiveType.Cylinder,world.transform,points[i]+Vector3.up*.25f,new Vector3(4,.25f,4),stone);p.interactionPoints[i]=marker.transform;var sign=new GameObject("地标文字",typeof(TextMesh));sign.transform.SetParent(marker.transform,false);sign.transform.localPosition=new Vector3(0,7,0);sign.transform.localScale=new Vector3(.25f,4,.25f);var text=sign.GetComponent<TextMesh>();text.text=p.interactionNames[i];text.font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",32);text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;text.fontSize=32;text.characterSize=.16f;text.anchor=TextAnchor.MiddleCenter;}
            p.checkpoints=new Transform[5];var gold=ArenaSetup.Material("试炼标记",new Color(1,.65f,.15f));for(int i=0;i<5;i++){var cp=ArenaSetup.Shape("疾行检查点_"+(i+1),PrimitiveType.Cylinder,world.transform,new Vector3(8+(i%2)*13,.3f,42+i*7),new Vector3(4,.3f,4),gold);UnityEngine.Object.DestroyImmediate(cp.GetComponent<Collider>());p.checkpoints[i]=cp.transform;cp.SetActive(false);}
            var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);foreach(var t in canvas.GetComponentsInChildren<Text>(true))t.font=font;
            inv.gameObject.SetActive(false);pause.gameObject.SetActive(false);new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            PrefabUtility.SaveAsPrefabAssetAndConnect(player,"Assets/_Game/Prefabs/PlayerExplorer.prefab",InteractionMode.AutomatedAction);PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,"Assets/_Game/Prefabs/ValleyHUD.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/Valley.unity");
            var arena=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Arena_Stone.unity");Camera.main.backgroundColor=new Color(.17f,.28f,.22f);GameObject.Find("青石擂台_场景布局").name="竹林擂台_场景布局";EditorSceneManager.SaveScene(arena,"Assets/_Game/Scenes/Arena_Bamboo.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/_Game/Scenes/Valley.unity",true),new EditorBuildSettingsScene("Assets/_Game/Scenes/Arena_Stone.unity",true),new EditorBuildSettingsScene("Assets/_Game/Scenes/Arena_Bamboo.unity",true),new EditorBuildSettingsScene("Assets/_Game/Scenes/GestureLab.unity",true),new EditorBuildSettingsScene("Assets/_Game/Scenes/Bootstrap.unity",true)};
            var practice=EditorSceneManager.OpenScene("Assets/_Game/Scenes/GestureLab.unity");var practiceCanvas=UnityEngine.Object.FindObjectOfType<Canvas>();var nav=practiceCanvas.gameObject.AddComponent<ReturnToValley>();var back=ArenaSetup.Button("返回归云谷",practiceCanvas.transform,"返回归云谷",new Vector2(0,350),new Vector2(220,48));back.GetComponentInChildren<Text>().font=font;UnityEventTools.AddPersistentListener(back.onClick,nav.Return);EditorSceneManager.SaveScene(practice);
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");AssetDatabase.SaveAssets();
        }
    }
}
