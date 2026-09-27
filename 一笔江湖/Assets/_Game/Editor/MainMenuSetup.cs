using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using Yibi.World;
using Yibi.UI;

namespace Yibi.Editor
{
    public static class MainMenuSetup
    {
        public static void Create()
        {
            const string path="Assets/_Game/Scenes/MainMenu.unity";if(File.Exists(path))return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);var camera=Camera.main;camera.transform.position=new Vector3(0,3,-8);camera.transform.LookAt(new Vector3(0,1.3f,0));camera.fieldOfView=42;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.2f,.24f);RenderSettings.ambientLight=new Color(.62f,.66f,.65f);
            var light=UnityEngine.Object.FindObjectOfType<Light>();light.transform.rotation=Quaternion.Euler(45,-25,0);light.intensity=1.2f;
            var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRigBuilder.Folder+"/侠客_劲装.prefab"));actor.transform.position=new Vector3(2,0,0);actor.transform.rotation=Quaternion.Euler(0,200,0);actor.transform.localScale=Vector3.one*1.25f;
            foreach(var item in new[]{new Vector3(3,0,3),new Vector3(-3,0,4)}){var tree=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Selected/Characters/桃树.prefab"));tree.transform.position=item;tree.transform.localScale=Vector3.one*.8f;}
            var platform=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Selected/Characters/修炼石台.prefab"));platform.transform.position=new Vector3(2,-1.2f,0);platform.transform.localScale=new Vector3(.6f,1,.6f);
            ArenaSetup.Shape("庭院地面",PrimitiveType.Cube,null,new Vector3(0,-1.5f,4),new Vector3(50,.3f,40),AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Selected/资源_擂台地砖.mat"));
            var canvas=new GameObject("MainMenuUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(LocalChineseFont),typeof(MainMenuPresenter));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            var p=canvas.GetComponent<MainMenuPresenter>();var panel=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/UI/武侠蓝纹面板.asset");var button=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/UI/青玉按钮.asset");
            var menu=ArenaSetup.Panel("主菜单",canvas.transform,new Vector2(-365,0),new Vector2(440,650),Color.white);menu.sprite=panel;menu.type=Image.Type.Sliced;
            var title=ArenaSetup.Label("作品名",menu.transform,"一 笔 江 湖",new Vector2(0,235),new Vector2(390,95),48);title.alignment=TextAnchor.MiddleCenter;
            var sub=ArenaSetup.Label("题记",menu.transform,"一笔连经脉 · 一招定乾坤",new Vector2(0,173),new Vector2(390,50),20);sub.alignment=TextAnchor.MiddleCenter;
            p.progress=ArenaSetup.Label("章节进度",menu.transform,"",new Vector2(0,120),new Vector2(350,40),18);p.progress.alignment=TextAnchor.MiddleCenter;
            UnityEngine.Events.UnityAction[] actions={p.Continue,p.Practice,p.Multiplayer,p.ToggleHelp,p.Quit};string[] names={"踏入归云谷","经脉练习","双人论武","操作说明","退出"};
            for(int i=0;i<5;i++){var b=ArenaSetup.Button("菜单_"+i,menu.transform,names[i],new Vector2(0,52-i*64),new Vector2(350,50));b.image.sprite=button;b.image.type=Image.Type.Sliced;b.image.color=Color.white;UnityEventTools.AddPersistentListener(b.onClick,actions[i]);if(i==0)p.continueText=b.GetComponentInChildren<Text>();}
            var help=ArenaSetup.Panel("操作说明",canvas.transform,new Vector2(220,0),new Vector2(670,520),Color.white);help.sprite=panel;help.type=Image.Type.Sliced;p.help=help.gameObject;
            var text=ArenaSetup.Label("操作",help.transform,"归云谷\nWASD 移动 · 左 Shift 奔跑\nE 与地标交互 · F 开启遗匣\nTab 秘籍行囊 · Esc 菜单\n\n回合战斗\n点击秘籍 → 按顺序连穴 → 松开评分 → 确认\n点穴接裂石：破绽联动\n回风接流火：灼伤联动\n内力不足时调息，生命受损可用清心",new Vector2(0,35),new Vector2(590,390),23);
            var close=ArenaSetup.Button("收起说明",help.transform,"收起说明",new Vector2(0,-200),new Vector2(250,50));close.image.sprite=button;close.image.type=Image.Type.Sliced;UnityEventTools.AddPersistentListener(close.onClick,p.ToggleHelp);help.gameObject.SetActive(false);
            var font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);foreach(var label in canvas.GetComponentsInChildren<Text>(true))label.font=font;
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,"Assets/_Game/Prefabs/MainMenuUI.prefab",InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene,path);
            var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=path).ToList();scenes.Insert(0,new EditorBuildSettingsScene(path,true));if(!scenes.Any(s=>s.path.EndsWith("AnimationWorkshop.unity")))scenes.Add(new EditorBuildSettingsScene("Assets/_Game/Scenes/AnimationWorkshop.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
            scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");var v=UnityEngine.Object.FindObjectOfType<ValleyPresenter>();var rect=v.pausePanel.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(450,340);foreach(var t in v.pausePanel.GetComponentsInChildren<RectTransform>())if(t.name=="继续")t.anchoredPosition=new Vector2(0,95);else if(t.name=="退出")t.anchoredPosition=new Vector2(0,-95);
            var back=ArenaSetup.Button("返回标题",v.pausePanel.transform,"保存并返回标题",Vector2.zero,new Vector2(360,65));back.image.sprite=button;back.image.type=Image.Type.Sliced;back.GetComponentInChildren<Text>().font=font;UnityEventTools.AddPersistentListener(back.onClick,v.ReturnToTitle);foreach(var t in v.pausePanel.GetComponentsInChildren<RectTransform>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(t);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.OpenScene(path);
        }
    }
}
