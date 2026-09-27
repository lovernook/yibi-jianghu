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

namespace Yibi.Editor
{
    public static class LobbySetup
    {
        private static InputField Input(string name,Transform parent,Vector2 pos,string value){var panel=ArenaSetup.Panel(name,parent,pos,new Vector2(400,55),new Color(.14f,.22f,.24f));var input=panel.gameObject.AddComponent<InputField>();input.textComponent=ArenaSetup.Label("输入",panel.transform,value,Vector2.zero,new Vector2(380,50),22);input.text=value;input.characterLimit=64;return input;}
        [MenuItem("一笔江湖/联机/创建联机大厅（仅首次）")]
        public static void Create()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save and exit Play first");
            if(File.Exists("Assets/_Game/Scenes/Lobby.unity"))throw new InvalidOperationException("Lobby exists");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);Camera.main.clearFlags=CameraClearFlags.SolidColor;Camera.main.backgroundColor=new Color(.07f,.13f,.16f);
            var canvas=new GameObject("RoomHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(LocalChineseFont),typeof(LobbyPresenter));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var p=canvas.GetComponent<LobbyPresenter>();ArenaSetup.Label("标题",canvas.transform,"一笔江湖 · 双人论武",new Vector2(0,280),new Vector2(1020,70),40);
            ArenaSetup.Label("说明",canvas.transform,"先启动随附本地服务；同机用 127.0.0.1，局域网填写服务所在电脑的地址。\n双方各运行一份客户端，创建或加入房间，装配后准备。PVP 开放全部六秘籍。",new Vector2(0,190),new Vector2(1020,100),21);
            p.address=Input("服务器地址",canvas.transform,new Vector2(-300,90),"127.0.0.1");p.roomCode=Input("房间码",canvas.transform,new Vector2(-300,15),"");p.roomCode.contentType=InputField.ContentType.IntegerNumber;p.roomCode.characterLimit=6;
            var create=ArenaSetup.Button("创建房间",canvas.transform,"创建房间",new Vector2(90,90),new Vector2(250,55));UnityEventTools.AddPersistentListener(create.onClick,p.Create);
            var join=ArenaSetup.Button("加入房间",canvas.transform,"输入六位房号后加入",new Vector2(90,15),new Vector2(250,55));UnityEventTools.AddPersistentListener(join.onClick,p.Join);
            p.status=ArenaSetup.Label("连接状态",canvas.transform,"未连接",new Vector2(0,-75),new Vector2(1040,70),23);
            p.loadoutButtons=new Button[3];for(int i=0;i<3;i++){p.loadoutButtons[i]=ArenaSetup.Button("秘籍槽"+i,canvas.transform,"",new Vector2(-390+i*260,-145),new Vector2(240,55));UnityEventTools.AddIntPersistentListener(p.loadoutButtons[i].onClick,p.Cycle,i);}
            var mind=ArenaSetup.Button("心法",canvas.transform,"",new Vector2(390,-145),new Vector2(240,55));p.mindsetText=mind.GetComponentInChildren<Text>();UnityEventTools.AddPersistentListener(mind.onClick,p.Mindset);
            p.readiness=ArenaSetup.Label("准备状态",canvas.transform,"",new Vector2(0,-210),new Vector2(1040,55),20);
            var ready=ArenaSetup.Button("准备",canvas.transform,"确认构筑 · 准备",new Vector2(-360,-280),new Vector2(320,60));UnityEventTools.AddPersistentListener(ready.onClick,p.Ready);
            var resume=ArenaSetup.Button("恢复",canvas.transform,"恢复断开的会话",new Vector2(0,-280),new Vector2(320,60));UnityEventTools.AddPersistentListener(resume.onClick,p.Resume);
            var leave=ArenaSetup.Button("离开",canvas.transform,"离开并返回山谷",new Vector2(360,-280),new Vector2(320,60));UnityEventTools.AddPersistentListener(leave.onClick,p.Leave);
            var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);foreach(var t in canvas.GetComponentsInChildren<Text>(true))t.font=font;
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,"Assets/_Game/Prefabs/RoomHUD.prefab",InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/Lobby.unity");
            var valley=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");var vp=UnityEngine.Object.FindObjectOfType<ValleyPresenter>();var link=ArenaSetup.Button("双人论武",vp.transform,"双人论武",new Vector2(220,310),new Vector2(210,54));link.GetComponentInChildren<Text>().font=font;UnityEventTools.AddPersistentListener(link.onClick,vp.OpenLobby);EditorSceneManager.SaveScene(valley);
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);scenes.Add(new EditorBuildSettingsScene("Assets/_Game/Scenes/Lobby.unity",true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
        }
        [MenuItem("一笔江湖/构建/完整 Windows Demo")]
        public static void Build()
        {
            ContentExporter.Export();string root=Directory.GetParent(Application.dataPath).Parent.FullName;Directory.CreateDirectory(Path.Combine(root,"Builds/Demo"));
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,Path.Combine(root,"Builds/Demo/YibiJianghu.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(Path.Combine(root,"Docs/Evidence/Demo-build.txt"),"result="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nwarnings="+report.summary.totalWarnings+"\nbytes="+report.summary.totalSize);
        }
    }
}
