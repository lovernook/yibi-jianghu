using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yibi.Battle;
using Yibi.UI;
using Yibi.Rules;

namespace Yibi.Editor
{
    public static class ArenaSetup
    {
        [MenuItem("一笔江湖/M2/构建 Windows 擂台")]
        public static void Build()
        {
            string root=Directory.GetParent(Application.dataPath).Parent.FullName;
            Directory.CreateDirectory(Path.Combine(root,"Builds/M2"));
            var report=BuildPipeline.BuildPlayer(new[]{"Assets/_Game/Scenes/Arena_Stone.unity","Assets/_Game/Scenes/GestureLab.unity","Assets/_Game/Scenes/Bootstrap.unity"},Path.Combine(root,"Builds/M2/YibiJianghu.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(Path.Combine(root,"Docs/Evidence/M2-build.txt"),"result="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nwarnings="+report.summary.totalWarnings+"\nbytes="+report.summary.totalSize);
        }
        private static Font font;
        private static Color paper=new Color(.94f,.90f,.79f),gold=new Color(.75f,.60f,.34f),ink=new Color(.055f,.10f,.12f,.94f);
        public static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        public static Image Panel(string name,Transform parent,Vector2 pos,Vector2 size,Color c){var i=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();i.color=c;return i;}
        public static Text Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,int fs=20){var t=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();t.font=font;t.fontSize=fs;t.color=paper;t.text=text;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
        public static Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size){var i=Panel(name,parent,pos,size,new Color(.16f,.24f,.25f));var b=i.gameObject.AddComponent<Button>();Label("文字",i.transform,text,Vector2.zero,size-Vector2.one*12,19).alignment=TextAnchor.MiddleCenter;return b;}
        public static Material Material(string name,Color color){string p="Assets/_Game/Art/Selected/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m!=null)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,p);return m;}
        public static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material m){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
        public static GameObject Actor(string name,Vector3 position,Color color)
        {
            var root=new GameObject(name);root.transform.position=position;var robe=Material(name+"_衣",color);var skin=Material("肤色",new Color(.76f,.58f,.4f));var dark=Material("墨色",new Color(.055f,.07f,.07f));
            Shape("长衫",PrimitiveType.Capsule,root.transform,new Vector3(0,1.15f,0),new Vector3(.85f,1.05f,.65f),robe);
            Shape("面部",PrimitiveType.Sphere,root.transform,new Vector3(0,2.35f,0),Vector3.one*.53f,skin);
            Shape("斗笠",PrimitiveType.Cylinder,root.transform,new Vector3(0,2.62f,0),new Vector3(1.1f,.08f,1.1f),dark);
            Shape("腰封",PrimitiveType.Cylinder,root.transform,new Vector3(0,1.05f,0),new Vector3(.9f,.12f,.7f),dark);
            Shape("佩剑",PrimitiveType.Cube,root.transform,new Vector3(.57f,.85f,0),new Vector3(.10f,1.5f,.13f),dark);
            return root;
        }
        [MenuItem("一笔江湖/M2/创建石台擂台（仅首次）")]
        public static void Create()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene and exit Play first");
            if(File.Exists("Assets/_Game/Scenes/Arena_Stone.unity"))throw new InvalidOperationException("Arena already exists; edit the saved scene");
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            Camera.main.transform.position=new Vector3(0,10,-16);Camera.main.transform.LookAt(new Vector3(0,1,1));Camera.main.fieldOfView=48;Camera.main.backgroundColor=new Color(.11f,.18f,.2f);Camera.main.clearFlags=CameraClearFlags.SolidColor;
            var light=UnityEngine.Object.FindObjectOfType<Light>();light.transform.rotation=Quaternion.Euler(55,-35,0);light.intensity=1.3f;RenderSettings.ambientLight=new Color(.55f,.62f,.64f);
            var stage=new GameObject("青石擂台_场景布局");var stone=Material("擂台青石",new Color(.36f,.43f,.43f));var wood=Material("红木",new Color(.30f,.14f,.09f));
            Shape("地面",PrimitiveType.Cube,stage.transform,new Vector3(0,-.4f,0),new Vector3(30,.5f,24),stone);
            Shape("圆形石台",PrimitiveType.Cylinder,stage.transform,Vector3.zero,new Vector3(13,.2f,10),stone);
            for(int i=0;i<6;i++)Shape("立柱_"+i,PrimitiveType.Cylinder,stage.transform,new Vector3(-10+i*4,2.5f,8),new Vector3(.6f,2.5f,.6f),wood);
            Shape("门楣",PrimitiveType.Cube,stage.transform,new Vector3(0,4.8f,8),new Vector3(22,.6f,.8f),wood);
            var art=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArtPreparation.Output+"/Environment_49-50.prefab"));art.name="远景_皇城建筑";art.transform.position=new Vector3(0,-2,70);art.transform.localScale=Vector3.one*.7f;
            foreach(var r in art.GetComponentsInChildren<Renderer>())if(r.sharedMaterial.mainTexture==null)r.enabled=false;
            var left=Actor("行者",new Vector3(-3.5f,.2f,0),new Color(.12f,.38f,.34f));var right=Actor("守卫",new Vector3(3.5f,.2f,1),new Color(.45f,.16f,.12f));
            PrefabUtility.SaveAsPrefabAsset(left,"Assets/_Game/Prefabs/BattleActor.prefab");
            var canvas=new GameObject("BattleHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(LocalChineseFont));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var p=canvas.AddComponent<BattlePresenter>();p.leftActor=left.transform;p.rightActor=right.transform;
            var top=Panel("双方状态",canvas.transform,new Vector2(0,285),new Vector2(1240,125),ink);
            p.leftStatus=Label("我方状态",top.transform,"",new Vector2(-385,0),new Vector2(440,110),23);p.rightStatus=Label("敌方状态",top.transform,"",new Vector2(385,0),new Vector2(440,110),23);
            Label("标题",top.transform,"一 笔 江 湖\n青石擂台",Vector2.zero,new Vector2(200,95),27).alignment=TextAnchor.MiddleCenter;
            p.turnLabel=Label("回合倒计时",canvas.transform,"准备",new Vector2(0,195),new Vector2(900,40),22);p.turnLabel.alignment=TextAnchor.MiddleCenter;
            var bottom=Panel("行动栏",canvas.transform,new Vector2(0,-288),new Vector2(1240,125),ink);
            p.skillButtons=new Button[3];for(int i=0;i<3;i++){var b=Button("秘籍槽"+i,bottom.transform,"",new Vector2(-475+i*205,0),new Vector2(190,88));UnityEventTools.AddIntPersistentListener(b.onClick,p.SelectSkill,i);p.skillButtons[i]=b;}
            p.basicButton=Button("吐纳掌",bottom.transform,"吐纳掌\n零耗能 · 6伤害",new Vector2(170,0),new Vector2(190,88));UnityEventTools.AddPersistentListener(p.basicButton.onClick,p.Basic);
            p.restButton=Button("调息",bottom.transform,"调息\n回复2内力",new Vector2(380,0),new Vector2(190,88));UnityEventTools.AddPersistentListener(p.restButton.onClick,p.Rest);
            var log=Panel("战斗记录",canvas.transform,new Vector2(-435,-90),new Vector2(370,220),ink);p.logLabel=Label("事件",log.transform,"",Vector2.zero,new Vector2(345,205),17);
            var draw=Panel("经脉施法面板",canvas.transform,new Vector2(375,0),new Vector2(465,460),ink);p.drawingPanel=draw.gameObject;
            var board=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/GestureBoard.prefab"),draw.transform);p.board=board.GetComponent<GestureBoard>();var br=(RectTransform)board.transform;br.anchorMin=br.anchorMax=new Vector2(.5f,.5f);br.anchoredPosition=new Vector2(0,35);br.sizeDelta=new Vector2(320,320);
            p.scoreLabel=Label("评分反馈",draw.transform,"",new Vector2(0,-150),new Vector2(440,55),17);
            p.confirmButton=Button("确认施放",draw.transform,"确认施放",new Vector2(100,-202),new Vector2(195,42));UnityEventTools.AddPersistentListener(p.confirmButton.onClick,p.Confirm);
            var clear=Button("清空重画",draw.transform,"清空重画",new Vector2(-105,-202),new Vector2(190,42));UnityEventTools.AddPersistentListener(clear.onClick,p.Clear);
            p.routes=new GestureTemplateSO[6];var rules=BattleContent.Routes();for(int i=0;i<6;i++){string path="Assets/_Game/Data/Gestures/"+BattleContent.Ids[i]+".asset";var t=AssetDatabase.LoadAssetAtPath<GestureTemplateSO>(path);if(t==null){t=ScriptableObject.CreateInstance<GestureTemplateSO>();t.stableId=rules[i].Id;t.displayName=BattleContent.Names[i];t.radius=.045f;t.nodes=new Vector2[rules[i].Nodes.Length];for(int j=0;j<t.nodes.Length;j++)t.nodes[j]=new Vector2((float)rules[i].Nodes[j].X,(float)rules[i].Nodes[j].Y);AssetDatabase.CreateAsset(t,path);}p.routes[i]=t;}
            var result=Panel("胜败结算",canvas.transform,Vector2.zero,new Vector2(580,280),ink);p.resultPanel=result.gameObject;p.resultLabel=Label("胜败",result.transform,"本场结束",new Vector2(0,70),new Vector2(530,80),36);p.resultLabel.alignment=TextAnchor.MiddleCenter;
            var restart=Button("再战",result.transform,"再战一场",new Vector2(-130,-70),new Vector2(225,65));UnityEventTools.AddPersistentListener(restart.onClick,p.Restart);
            var back=Button("返回",result.transform,"返回练功",new Vector2(130,-70),new Vector2(225,65));UnityEventTools.AddPersistentListener(back.onClick,p.ReturnToPractice);
            draw.gameObject.SetActive(false);result.gameObject.SetActive(false);new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,"Assets/_Game/Prefabs/BattleHUD.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/Arena_Stone.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/_Game/Scenes/Arena_Stone.unity",true),new EditorBuildSettingsScene("Assets/_Game/Scenes/GestureLab.unity",true),new EditorBuildSettingsScene("Assets/_Game/Scenes/Bootstrap.unity",true)};AssetDatabase.SaveAssets();
        }
    }
}
