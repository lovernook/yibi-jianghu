using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yibi.App;
using Yibi.UI;
using Yibi.World;

namespace Yibi.Editor
{
    // Creates saved authoring assets only. It never builds a screen during ordinary gameplay.
    public static class FrameworkStageASetup
    {
        const string RootPath="Assets/_Game/Resources/App/GameRoot.prefab";
        static readonly Color Ink=new Color(.035f,.075f,.09f,.96f);
        static readonly Color Paper=new Color(.94f,.91f,.81f);
        static readonly Color Gold=new Color(.76f,.61f,.36f);
        static Font font;

        [MenuItem("一笔江湖/3.0/构建框架验证版")]
        public static void Build()
        {
            PolishEnvironmentSetup.Guard();
            ContentExporter.Export();
            var root=Directory.GetParent(Application.dataPath).Parent.FullName;
            var output=Path.Combine(root,"Builds/FrameworkStageA");Directory.CreateDirectory(output);
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,Path.Combine(output,"YibiJianghu.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(Path.Combine(root,"Docs/Evidence/FrameworkStageA-build.txt"),
                "utc="+DateTime.UtcNow.ToString("o")+"\nresult="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nwarnings="+report.summary.totalWarnings+"\nbytes="+report.summary.totalSize+"\nseconds="+report.summary.totalTime.TotalSeconds);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("框架验证版构建失败，请查看构建报告。");
        }

        [MenuItem("一笔江湖/3.0/保存框架入口与主页")]
        public static void Install()
        {
            PolishEnvironmentSetup.Guard();font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);
            Directory.CreateDirectory("Assets/_Game/Resources/App");AssetDatabase.Refresh();
            SaveRoot();SaveMenu();AssetDatabase.SaveAssets();
        }

        static void SaveRoot()
        {
            var root=new GameObject("GameRoot · 应用流程",typeof(GameRoot));
            try{
                var canvas=Canvas("LoadingCanvas · 输入遮罩",root.transform,200);
                var view=canvas.AddComponent<LoadingOverlay>();root.GetComponent<GameRoot>().loadingOverlay=view;
                var panel=Panel("行程加载",canvas.transform,Vector2.zero,new Vector2(1280,720),Ink);
                Stretch(panel.rectTransform);view.panel=panel.gameObject;
                Label("标记",panel.transform,"一 笔 江 湖",new Vector2(0,136),new Vector2(600,70),38,Paper,TextAnchor.MiddleCenter);
                Label("题记",panel.transform,"收心凝神 · 下一程将启",new Vector2(0,82),new Vector2(600,36),17,Gold,TextAnchor.MiddleCenter);
                view.status=Label("加载状态",panel.transform,"正在准备行程",new Vector2(0,15),new Vector2(740,65),22,Paper,TextAnchor.MiddleCenter);
                var bar=Panel("进度条",panel.transform,new Vector2(0,-45),new Vector2(500,4),new Color(.2f,.26f,.26f));
                var slider=bar.gameObject.AddComponent<Slider>();slider.interactable=false;slider.transition=Selectable.Transition.None;
                var fill=Panel("填充",bar.transform,Vector2.zero,new Vector2(500,4),Gold);Stretch(fill.rectTransform);slider.fillRect=fill.rectTransform;slider.targetGraphic=fill;view.progressSlider=slider;
                view.progressText=Label("加载百分比",panel.transform,"0%",new Vector2(0,-77),new Vector2(500,32),16,Paper,TextAnchor.MiddleCenter);
                view.retryButton=Button("重新尝试",panel.transform,"重新尝试",new Vector2(-115,-142),new Vector2(210,46),true);
                view.cancelButton=Button("留在当前界面",panel.transform,"留在当前界面",new Vector2(115,-142),new Vector2(210,46),false);
                UnityEventTools.AddPersistentListener(view.retryButton.onClick,view.Retry);UnityEventTools.AddPersistentListener(view.cancelButton.onClick,view.Cancel);
                panel.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,RootPath);
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        static void SaveMenu()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
            var old=UnityEngine.Object.FindObjectOfType<MainMenuPresenter>();if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var existing=UnityEngine.Object.FindObjectOfType<GameRoot>();if(existing!=null)UnityEngine.Object.DestroyImmediate(existing.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RootPath));
            var canvas=Canvas("MainMenuUI · 归云卷",null,0);var presenter=canvas.AddComponent<MainMenuPresenter>();
            var left=Panel("墨色导航",canvas.transform,new Vector2(-410,0),new Vector2(460,720),Ink);
            Panel("金线",left.transform,new Vector2(206,0),new Vector2(1,610),new Color(Gold.r,Gold.g,Gold.b,.5f));
            Label("卷次",left.transform,"归 云  ·  第 一 章",new Vector2(0,290),new Vector2(356,36),15,Gold);
            Label("作品名",left.transform,"一笔江湖",new Vector2(0,218),new Vector2(356,86),58,Paper);
            Label("题记",left.transform,"一笔运气，一招定局。",new Vector2(0,154),new Vector2(356,38),19,new Color(.74f,.78f,.75f));
            Panel("分隔",left.transform,new Vector2(-104,111),new Vector2(146,1),Gold);
            presenter.progress=Label("旅程进度",left.transform,"归云篇 · 初识经脉",new Vector2(0,80),new Vector2(356,40),16,new Color(.73f,.76f,.71f));
            var primary=Button("继续旅程",left.transform,"踏入归云谷",new Vector2(0,19),new Vector2(356,56),true);presenter.continueText=primary.GetComponentInChildren<Text>();UnityEventTools.AddPersistentListener(primary.onClick,presenter.Continue);
            var practice=Button("六脉研习",left.transform,"六脉研习",new Vector2(0,-53),new Vector2(356,52),false);UnityEventTools.AddPersistentListener(practice.onClick,presenter.Practice);
            var versus=Button("双人论武",left.transform,"双人论武",new Vector2(0,-117),new Vector2(356,52),false);UnityEventTools.AddPersistentListener(versus.onClick,presenter.Multiplayer);
            Label("入口说明",left.transform,"独行历练  /  练习运笔  /  与友切磋",new Vector2(0,-177),new Vector2(356,35),14,new Color(.64f,.71f,.7f));
            var help=Button("操作指引",left.transform,"操作指引",new Vector2(-92,-239),new Vector2(174,40),false);UnityEventTools.AddPersistentListener(help.onClick,presenter.ToggleHelp);
            var quit=Button("退出",left.transform,"退出",new Vector2(92,-239),new Vector2(174,40),false);UnityEventTools.AddPersistentListener(quit.onClick,presenter.Quit);
            Label("落款",left.transform,"归云谷中，且行且悟。",new Vector2(0,-299),new Vector2(356,28),13,new Color(.58f,.66f,.64f));
            var region=Panel("右下题签",canvas.transform,new Vector2(415,-280),new Vector2(320,66),new Color(.025f,.06f,.075f,.76f));
            Label("地名",region.transform,"归云谷  /  晴",new Vector2(0,11),new Vector2(284,28),19,Paper);
            Label("地名副题",region.transform,"山河在前，江湖在笔下。",new Vector2(0,-14),new Vector2(284,25),13,new Color(.73f,.78f,.73f));
            var modal=Panel("操作指引弹窗",canvas.transform,Vector2.zero,new Vector2(1280,720),new Color(0,0,0,.7f));Stretch(modal.rectTransform);presenter.help=modal.gameObject;
            var card=Panel("指引正文",modal.transform,Vector2.zero,new Vector2(720,540),Ink);
            Label("指引标题",card.transform,"行走江湖",new Vector2(0,205),new Vector2(624,60),32,Paper);
            Label("指引内容",card.transform,"归云探索\nWASD 行走 · Shift 奔跑 · 滚轮缩放\nE 交谈 · F 开匣 · Tab 行囊 · Esc 菜单\n\n经脉施法\n选择秘籍 → 依序连穴 → 松开查看评分 → 确认\n点穴后接裂石可利用破绽；内力不足时调息。\n\n双人论武\n先启动本地服务，双方创建／加入相同房号后准备。",new Vector2(0,-1),new Vector2(624,330),19,Paper);
            var close=Button("返回主页",card.transform,"明白了",new Vector2(0,-210),new Vector2(280,48),true);UnityEventTools.AddPersistentListener(close.onClick,presenter.ToggleHelp);modal.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,"Assets/_Game/Prefabs/MainMenuUI.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static GameObject Canvas(string name,Transform parent,int order)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));g.transform.SetParent(parent,false);g.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;g.GetComponent<Canvas>().sortingOrder=order;
            var scaler=g.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;g.AddComponent<LocalChineseFont>();return g;
        }
        static Image Panel(string name,Transform parent,Vector2 position,Vector2 size,Color color){return ArenaSetup.Panel(name,parent,position,size,color);}
        static Text Label(string name,Transform parent,string value,Vector2 position,Vector2 size,int point,Color color,TextAnchor align=TextAnchor.MiddleLeft)
        {var t=ArenaSetup.Label(name,parent,value,position,size,point);t.font=font;t.color=color;t.alignment=align;t.raycastTarget=false;return t;}
        static Button Button(string name,Transform parent,string value,Vector2 position,Vector2 size,bool primary)
        {
            var b=ArenaSetup.Button(name,parent,value,position,size);b.image.color=primary?Gold:new Color(.12f,.18f,.19f,.95f);var colors=b.colors;colors.highlightedColor=new Color(1.15f,1.15f,1.08f);colors.pressedColor=new Color(.7f,.75f,.72f);colors.disabledColor=new Color(.4f,.45f,.45f);b.colors=colors;
            var t=b.GetComponentInChildren<Text>();t.font=font;t.fontSize=primary?22:18;t.color=primary?new Color(.07f,.10f,.10f):Paper;t.raycastTarget=false;return b;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
