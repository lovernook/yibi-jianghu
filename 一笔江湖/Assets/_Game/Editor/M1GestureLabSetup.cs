using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yibi.Rules;
using Yibi.UI;

namespace Yibi.Editor
{
    public static class M1GestureLabSetup
    {
        public const string ScenePath="Assets/_Game/Scenes/GestureLab.unity";
        private static readonly Color Ink=new Color(.075f,.12f,.14f,.96f), Gold=new Color(.79f,.66f,.4f), Paper=new Color(.90f,.91f,.85f);
        private static Font font;
        private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(.5f,.5f);rt.sizeDelta=size;rt.anchoredPosition=pos;return rt;
        }
        private static Image Panel(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
        {var image=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();image.color=color;return image;}
        private static Text Label(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var text=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.color=color;
            text.alignment=align;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        private static Button Button(string name,Transform parent,string caption,Vector2 pos,Vector2 size)
        {
            var image=Panel(name,parent,pos,size,new Color(.13f,.20f,.22f));var button=image.gameObject.AddComponent<Button>();
            var colors=button.colors;colors.highlightedColor=new Color(.8f,.93f,.87f);colors.pressedColor=new Color(.6f,.8f,.72f);button.colors=colors;
            Label("文字",image.transform,caption,Vector2.zero,size-Vector2.one*16,20,Paper,TextAnchor.MiddleCenter);return button;
        }
        private static void Stretch(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        private static Material Mat(string name,Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;
            AssetDatabase.CreateAsset(material,"Assets/_Game/Art/"+name+".mat");return material;
        }
        private static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }

        [MenuItem("一笔江湖/M1/创建练功原型（仅首次）")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("GestureLab already exists. Edit saved objects instead of regenerating.");
            foreach(string dir in new[]{"Data/Gestures","Art","Prefabs","Scenes"})Directory.CreateDirectory("Assets/_Game/"+dir);
            AssetDatabase.Refresh();font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            var rules=StarterRoutes.Create();var routes=new GestureTemplateSO[3];
            string[] names={"点穴指","裂石掌","回风诀"};string[] descriptions={"四穴起势 · 折线入门\n沿金色经脉依次连通 1—4。","五穴贯劲 · 转折练习\n注意折返处，不要跨过穴位。","六穴回风 · 弧线练习\n保持连贯，不以速度争高分。"};
            for(int i=0;i<3;i++)
            {
                var t=ScriptableObject.CreateInstance<GestureTemplateSO>();t.stableId=rules[i].Id;t.displayName=names[i];t.description=descriptions[i];t.radius=.045f;
                t.nodes=new Vector2[rules[i].Nodes.Length];for(int n=0;n<t.nodes.Length;n++)t.nodes[n]=new Vector2((float)rules[i].Nodes[n].X,(float)rules[i].Nodes[n].Y);
                AssetDatabase.CreateAsset(t,"Assets/_Game/Data/Gestures/"+rules[i].Id+".asset");routes[i]=t;
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var camera=Camera.main;camera.transform.position=new Vector3(9,12,-15);camera.transform.LookAt(new Vector3(0,0,1));camera.fieldOfView=48;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.17f,.18f);
            var light=UnityEngine.Object.FindObjectOfType<Light>();light.transform.rotation=Quaternion.Euler(48,-32,0);light.intensity=1.2f;
            RenderSettings.ambientLight=new Color(.48f,.56f,.55f);
            var stone=Mat("灰盒_青石",new Color(.34f,.43f,.43f));var jade=Mat("灰盒_衣衫",new Color(.20f,.43f,.39f));var wood=Mat("灰盒_木桩",new Color(.47f,.32f,.20f));
            var world=new GameObject("练功台_灰盒（可替换美术）");
            Shape("地基",PrimitiveType.Cube,world.transform,new Vector3(0,-.3f,0),new Vector3(20,.6f,16),stone);
            Shape("练功石台",PrimitiveType.Cylinder,world.transform,new Vector3(0,.15f,0),new Vector3(6,.15f,6),stone);
            var actor=Shape("练功弟子_胶囊占位",PrimitiveType.Capsule,world.transform,new Vector3(-2,1.3f,-1),new Vector3(.8f,1,.8f),jade);
            PrefabUtility.SaveAsPrefabAsset(actor,"Assets/_Game/Prefabs/PracticeActor.prefab");
            for(int i=0;i<3;i++)
            {
                var dummy=new GameObject("木桩_"+(i+1));dummy.transform.SetParent(world.transform,false);dummy.transform.localPosition=new Vector3(-3+i*3,0,4);
                Shape("桩身",PrimitiveType.Cylinder,dummy.transform,new Vector3(0,1.2f,0),new Vector3(.5f,1.2f,.5f),wood);
                Shape("横臂",PrimitiveType.Cube,dummy.transform,new Vector3(0,1.8f,0),new Vector3(1.6f,.2f,.2f),wood);
            }
            for(int i=0;i<4;i++)Shape("石柱_"+i,PrimitiveType.Cube,world.transform,new Vector3(i%2==0?-8:8,1.5f,i<2?-6:6),new Vector3(.7f,3,.7f),stone);
            PrefabUtility.SaveAsPrefabAsset(world,"Assets/_Game/Prefabs/PracticePlatform.prefab");

            var canvasGo=new GameObject("GestureLabCanvas_练功界面",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=1;
            var veil=Panel("背景轻遮罩",canvas.transform,Vector2.zero,new Vector2(1440,900),new Color(.025f,.06f,.07f,.42f));Stretch(veil.rectTransform);veil.raycastTarget=false;
            var presenter=canvasGo.AddComponent<GestureLabPresenter>();presenter.routes=routes;
            Label("主标题",canvas.transform,"一 笔 江 湖",new Vector2(-470,377),new Vector2(470,62),42,Paper);
            Label("阶段说明",canvas.transform,"练功台  /  穴位路线原型",new Vector2(485,377),new Vector2(380,45),21,Gold,TextAnchor.MiddleRight);
            Panel("顶部金线",canvas.transform,new Vector2(0,326),new Vector2(1390,2),Gold).raycastTarget=false;
            var left=Panel("秘籍选择面板",canvas.transform,new Vector2(-550,-35),new Vector2(280,680),Ink);
            Label("选择提示",left.transform,"壹  ·  选择功法",new Vector2(0,285),new Vector2(232,40),23,Gold);
            presenter.routeHighlights=new Image[3];
            for(int i=0;i<3;i++)
            {
                var b=Button("路线_"+names[i],left.transform,"0"+(i+1)+"    "+names[i],new Vector2(0,212-i*86),new Vector2(232,66));
                UnityEventTools.AddIntPersistentListener(b.onClick,presenter.SelectRoute,i);presenter.routeHighlights[i]=b.GetComponent<Image>();
            }
            presenter.routeDescription=Label("路线说明",left.transform,descriptions[0],new Vector2(0,-72),new Vector2(232,100),19,Paper);
            var clear=Button("清空重画",left.transform,"清空重画  ·  Esc",new Vector2(0,-185),new Vector2(232,48));UnityEventTools.AddPersistentListener(clear.onClick,presenter.Clear);
            var sample=Button("固定样本",left.transform,"下一个固定样本",new Vector2(0,-245),new Vector2(232,48));UnityEventTools.AddPersistentListener(sample.onClick,presenter.NextSample);
            var save=Button("保存手绘",left.transform,"保存当前手绘",new Vector2(0,-305),new Vector2(232,48));UnityEventTools.AddPersistentListener(save.onClick,presenter.SaveHumanSample);

            var boardBg=Panel("GestureBoard_经脉画板",canvas.transform,new Vector2(-65,-20),new Vector2(620,620),new Color(.035f,.09f,.11f,.97f));
            boardBg.gameObject.AddComponent<RectMask2D>();var board=boardBg.gameObject.AddComponent<GestureBoard>();board.template=routes[0];presenter.board=board;
            for(int i=1;i<10;i++)
            {
                Panel("网格_横_"+i,board.transform,new Vector2(0,-310+i*62),new Vector2(620,1),new Color(.13f,.22f,.23f,.65f)).raycastTarget=false;
                Panel("网格_纵_"+i,board.transform,new Vector2(-310+i*62,0),new Vector2(1,620),new Color(.13f,.22f,.23f,.65f)).raycastTarget=false;
            }
            var target=Rect("目标金线",board.transform,Vector2.zero,new Vector2(620,620));Stretch(target);board.targetLine=target.gameObject.AddComponent<PolylineGraphic>();board.targetLine.color=Gold;board.targetLine.thickness=3;board.targetLine.raycastTarget=false;
            var stroke=Rect("玩家青线",board.transform,Vector2.zero,new Vector2(620,620));Stretch(stroke);board.playerLine=stroke.gameObject.AddComponent<PolylineGraphic>();board.playerLine.color=new Color(.48f,.94f,.84f);board.playerLine.thickness=5;board.playerLine.raycastTarget=false;
            board.nodeMarkers=new RectTransform[7];board.nodeLabels=new Text[7];
            for(int i=0;i<7;i++)
            {
                var node=Rect("穴位_"+(i+1),board.transform,Vector2.zero,Vector2.one*55.8f);var ring=node.gameObject.AddComponent<MeridianNodeGraphic>();ring.color=Paper;ring.raycastTarget=false;
                board.nodeMarkers[i]=node;board.nodeLabels[i]=Label("序号",node,(i+1).ToString(),Vector2.zero,Vector2.one*45,24,Paper,TextAnchor.MiddleCenter);
            }
            board.PreviewTemplate();PrefabUtility.SaveAsPrefabAsset(board.nodeMarkers[0].gameObject,"Assets/_Game/Prefabs/MeridianNode.prefab");
            PrefabUtility.SaveAsPrefabAsset(board.gameObject,"Assets/_Game/Prefabs/GestureBoard.prefab");
            presenter.routeTitle=Label("当前功法",canvas.transform,names[0],new Vector2(-65,302),new Vector2(600,40),22,Paper,TextAnchor.MiddleCenter);
            presenter.sampleText=Label("样本来源",canvas.transform,"真人手绘  ·  等待输入",new Vector2(-65,-354),new Vector2(620,36),17,Gold,TextAnchor.MiddleCenter);

            var right=Panel("评分面板",canvas.transform,new Vector2(490,-35),new Vector2(370,680),Ink);
            Label("评分标题",right.transform,"贰  ·  运功评鉴",new Vector2(0,285),new Vector2(318,40),23,Gold);
            presenter.scoreText=Label("总分",right.transform,"—",new Vector2(0,204),new Vector2(320,100),64,Paper,TextAnchor.MiddleCenter);
            presenter.gradeText=Label("境界",right.transform,"静心 · 起笔",new Vector2(0,141),new Vector2(318,44),24,Gold,TextAnchor.MiddleCenter);
            presenter.metricBars=new Image[3];presenter.metricValues=new Text[3];string[] metricNames={"形状贴合","行进顺序","线长比例"};
            for(int i=0;i<3;i++)
            {
                float y=81-i*50;Label("分项名_"+i,right.transform,metricNames[i],new Vector2(-90,y),new Vector2(136,30),18,Paper);
                presenter.metricValues[i]=Label("分项值_"+i,right.transform,"—",new Vector2(121,y),new Vector2(68,30),18,Gold,TextAnchor.MiddleRight);
                var back=Panel("分项底条_"+i,right.transform,new Vector2(0,y-19),new Vector2(318,4),new Color(.18f,.26f,.27f));back.raycastTarget=false;
                var bar=Panel("分项进度_"+i,back.transform,Vector2.zero,new Vector2(318,4),Gold);bar.type=Image.Type.Filled;bar.fillMethod=Image.FillMethod.Horizontal;bar.fillAmount=0;bar.raycastTarget=false;presenter.metricBars[i]=bar;
            }
            presenter.detailsText=Label("评分解释",right.transform,"从 1 号穴位起笔，依次经过全部穴位。\n松开左键后自动评分。",new Vector2(0,-197),new Vector2(318,254),17,Paper,TextAnchor.UpperLeft);
            presenter.statusText=Label("操作状态",canvas.transform,"左键按住绘制 · 松开评分 · Esc 取消",new Vector2(0,-414),new Vector2(1390,46),18,Paper,TextAnchor.MiddleCenter);
            canvasGo.AddComponent<LocalChineseFont>();canvasGo.AddComponent<GestureLabSmoke>();
            PrefabUtility.SaveAsPrefabAsset(canvasGo,"Assets/_Game/Prefabs/GestureLabUI.prefab");
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(world,"Create practice platform");Undo.RegisterCreatedObjectUndo(canvasGo,"Create gesture UI");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene(M0Environment.ScenePath,true)};
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();Selection.activeGameObject=board.gameObject;
            Debug.Log("YIBI_M1_SCENE_SAVED "+ScenePath);
        }
        [MenuItem("一笔江湖/M1/构建 Windows 练功原型")]
        public static void Build()
        {
            string root=Directory.GetParent(Application.dataPath).Parent.FullName;
            Directory.CreateDirectory(Path.Combine(root,"Builds/M1"));
            var report=BuildPipeline.BuildPlayer(new[]{ScenePath,M0Environment.ScenePath},Path.Combine(root,"Builds/M1/YibiJianghu.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(Path.Combine(root,"Docs/Evidence/M1-build.txt"),"result="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nwarnings="+report.summary.totalWarnings+"\nbytes="+report.summary.totalSize);
            Debug.Log("YIBI_M1_BUILD "+report.summary.result);
        }
    }
}
