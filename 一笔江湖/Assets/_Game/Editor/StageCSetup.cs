using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Battle;
using Yibi.UI;

namespace Yibi.Editor
{
    // Explicit editor authoring: every fixed object and callback is saved, not rebuilt in a player.
    public static class StageCSetup
    {
        const string Content = "Assets/_Game/Content/Presentation";
        const string HudPath = "Assets/_Game/Prefabs/UI/StageCBattleHUD.prefab";
        static readonly Color Ink = new Color(.025f,.060f,.073f,1f);
        static readonly Color Card = new Color(.055f,.105f,.115f,.98f);
        static readonly Color Paper = new Color(.94f,.91f,.82f);
        static readonly Color Muted = new Color(.64f,.76f,.74f);
        static readonly Color Gold = new Color(.80f,.64f,.37f);
        static Font font;
        static SystemFontProfile fonts;
        static Sprite buttonSkin;
        static BattlePresentationProfile presentation;
        static NpcTacticProfile[] tactics;

        [MenuItem("一笔江湖/3.0/阶段C/保存战斗与经脉界面（首次迁移）")]
        public static void Install()
        {
            PolishEnvironmentSetup.Guard();
            Directory.CreateDirectory(Content); AssetDatabase.Refresh();
            fonts=AssetDatabase.LoadAssetAtPath<SystemFontProfile>("Assets/_Game/Content/UI/GuiyunSystemFontProfile.asset");
            if(fonts==null)throw new InvalidOperationException("Stage B font profile is required.");
            font=SavedChineseFont.GetSharedFont(fonts);
            buttonSkin=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/UI/青玉按钮.asset");
            presentation=Seed<BattlePresentationProfile>(Content+"/GuiyunBattlePresentation.asset",p=>p.techniques=BattlePresentationProfile.DefaultTechniques());
            tactics=new NpcTacticProfile[2];
            for(int i=0;i<2;i++){int strategy=i;tactics[i]=Seed<NpcTacticProfile>(Content+"/NpcTactic_"+i+".asset",p=>{p.displayName=strategy==0?"听风守卫":"问心守卫";p.priorities=NpcTacticProfile.DefaultRules(strategy);});}
            SaveArena("Arena_Stone",true);
            SaveArena("Arena_Bamboo",false);
            SavePractice();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Arena_Stone.unity");
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<BattlePresentationProfile>(Content+"/GuiyunBattlePresentation.asset");
        }

        static T Seed<T>(string path,Action<T> seed) where T:ScriptableObject
        {
            var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value!=null)return value;
            value=ScriptableObject.CreateInstance<T>();seed(value);AssetDatabase.CreateAsset(value,path);return value;
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static Image Panel(string name,Transform parent,float x,float y,float w,float h,Color color,bool raycast=false)
        {
            var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;
        }
        static Text Label(string name,Transform parent,string text,float x,float y,float w,float h,int size=20,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.color=color??Paper;
            t.text=text;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        static Button Button(string name,Transform parent,string text,float x,float y,float w,float h)
        {
            var image=Panel(name,parent,x,y,w,h,new Color(.13f,.28f,.30f),true);image.sprite=buttonSkin;image.type=Image.Type.Sliced;
            var b=image.gameObject.AddComponent<Button>();var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.91f,.68f);colors.pressedColor=new Color(.61f,.85f,.86f);colors.disabledColor=new Color(.42f,.47f,.48f);b.colors=colors;
            Label("文字",b.transform,text,0,0,w-18,h-10,21,null,TextAnchor.MiddleCenter);return b;
        }
        static void Icon(string name,Transform parent,Sprite sprite,float x,float y,float size,Color color)
        {
            var image=Panel(name,parent,x,y,size,size,color);image.sprite=sprite;image.preserveAspect=true;
        }
        static Image Meter(Transform parent,string name,float x,float y,Color color)
        {
            var back=Panel(name+"底",parent,x,y,416,7,new Color(.10f,.19f,.20f));
            var fill=Panel(name,back.transform,0,0,416,7,color);var r=fill.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,.5f);r.anchoredPosition=Vector2.zero;return fill;
        }

        static void SaveArena(string name,bool makePrefab)
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");
            var previous=UnityEngine.Object.FindObjectOfType<BattlePresenter>();
            if(previous==null)throw new InvalidOperationException("Battle presenter missing in "+name);
            var left=previous.leftActor;var right=previous.rightActor;var routes=previous.routes;var icons=previous.techniqueIcons;
            if(PrefabUtility.IsPartOfPrefabInstance(previous.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(previous.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
            GameObject canvas;
            if(makePrefab){canvas=new GameObject("BattleHUD · 经脉论武",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));CreateHUD(canvas);}
            else canvas=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
            var p=canvas.GetComponent<BattlePresenter>();p.leftActor=left;p.rightActor=right;p.routes=routes;p.techniqueIcons=icons;p.presentation=presentation;p.npcTactics=tactics;
            canvas.transform.Find("战局顶栏/场景题签").GetComponent<Text>().text=name=="Arena_Stone"?"问 心 台  ·  论 武":"听 风 台  ·  论 武";
            var f=p.feedback;f.attackEffects=new SequenceEffect[2];f.supportEffects=new SequenceEffect[2];
            var actors=new[]{left,right};
            for(int i=0;i<actors.Length;i++){
                f.supportEffects[i]=actors[i].GetComponentsInChildren<SequenceEffect>(true).FirstOrDefault(x=>x.name!="刀光 · 命中表现");
                var attack=actors[i].Find("刀光 · 命中表现");
                if(attack==null){var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Selected/StageC/Effects/AttackSequence.prefab");if(source!=null){var g=(GameObject)PrefabUtility.InstantiatePrefab(source);g.name="刀光 · 命中表现";g.transform.SetParent(actors[i],false);g.transform.localPosition=new Vector3(i==0?1.1f:-1.1f,1.3f,0);g.transform.localScale=Vector3.one*3.8f;attack=g.transform;}}
                if(attack!=null)f.attackEffects[i]=attack.GetComponent<SequenceEffect>();
            }
            var savedAttacks=f.attackEffects;var savedSupport=f.supportEffects;
            if(makePrefab)PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,HudPath,InteractionMode.AutomatedAction);
            // Scene actors are intentionally not serialized into the reusable UI prefab asset.
            // Reapply after connecting, then persist these references as instance overrides.
            p.leftActor=left;p.rightActor=right;f.attackEffects=savedAttacks;f.supportEffects=savedSupport;
            foreach(var component in canvas.GetComponentsInChildren<MonoBehaviour>(true))if(component!=null){EditorUtility.SetDirty(component);PrefabUtility.RecordPrefabInstancePropertyModifications(component);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        static void CreateHUD(GameObject root)
        {
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1;
            var p=root.AddComponent<BattlePresenter>();var f=root.AddComponent<BattleFeedback>();var view=root.AddComponent<BattleHUDView>();
            p.feedback=f;p.hud=view;f.battle=p;view.battle=p;root.AddComponent<BattleSmoke>();
            var header=Panel("战局顶栏",root.transform,0,330,1536,205,Ink);
            Panel("金线",header.transform,0,-102,1536,2,Gold);
            Label("场景题签",header.transform,"问 心 台  ·  论 武",0,77,450,38,25,Gold,TextAnchor.MiddleCenter);
            p.turnLabel=Label("回合倒计时",header.transform,"第 1 轮 · 你的回合",0,38,500,34,22,null,TextAnchor.MiddleCenter);
            view.phaseLabel=Label("行动阶段",header.transform,"观察局势，再选一招",0,5,490,26,18,Muted,TextAnchor.MiddleCenter);
            p.leftStatus=Label("席位一状态",header.transform,"行者",-530,60,438,79,21);
            p.rightStatus=Label("席位二状态",header.transform,"守卫",530,60,438,79,21);
            f.healthFills=new[]{Meter(header.transform,"我方生命",-530,5,new Color(.24f,.78f,.60f)),Meter(header.transform,"对手生命",530,5,new Color(.90f,.42f,.31f))};
            f.energyFills=new[]{Meter(header.transform,"我方内力",-530,-7,new Color(.34f,.68f,.84f)),Meter(header.transform,"对手内力",530,-7,new Color(.34f,.68f,.84f))};
            view.leftEffects=Label("我方状态持续",header.transform,"",-530,-60,440,87,15,Muted,TextAnchor.UpperLeft);
            view.rightEffects=Label("对手状态持续",header.transform,"",530,-60,440,87,15,Muted,TextAnchor.UpperLeft);
            var leave=Button("离开擂台",header.transform,"返回归云谷",0,-60,230,38);UnityEventTools.AddPersistentListener(leave.onClick,p.ReturnToPractice);
            var strategy=Panel("观势手札",root.transform,-566,138,404,152,Ink);
            Label("题签",strategy.transform,"观 势  /  当前局面",0,53,362,28,18,Gold);
            view.intentLabel=Label("对手倾向",strategy.transform,"",0,-10,362,102,18);
            var record=Panel("本回合与战斗记录",root.transform,-566,-105,404,310,Ink);
            Label("题签",record.transform,"复 盘  /  已结算结果",0,137,362,28,18,Gold);
            view.settlementLabel=Label("上一行动结果",record.transform,"等待第一招",0,48,362,148,17,Muted);
            Panel("分隔线",record.transform,0,-31,362,1,Gold);
            var viewport=Rect("记录滚动区域",record.transform,0,-92,362,110);viewport.gameObject.AddComponent<RectMask2D>();
            var scrollHit=viewport.gameObject.AddComponent<Image>();scrollHit.color=Color.clear;scrollHit.raycastTarget=true;
            p.logLabel=Label("战斗事件",viewport,"",0,0,362,110,14,null,TextAnchor.UpperLeft);
            p.logLabel.rectTransform.anchorMin=p.logLabel.rectTransform.anchorMax=p.logLabel.rectTransform.pivot=new Vector2(.5f,1);
            var fit=p.logLabel.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=p.logLabel.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=20;
            var bottom=Panel("行动与构筑",root.transform,0,-374,1536,124,Ink);
            Panel("金线",bottom.transform,0,62,1536,2,Gold);
            var combo=Panel("构筑提要",root.transform,-285,-285,966,43,Ink);
            view.comboLabel=Label("构筑提示",combo.transform,"",0,0,922,39,17,Gold,TextAnchor.MiddleCenter);
            p.skillButtons=new Button[3];p.skillIcons=new Image[3];view.skillHints=new Text[3];
            for(int i=0;i<3;i++){
                float x=-587+i*285;var b=Button("秘籍槽"+i,bottom.transform,"秘籍",x,8,267,70);UnityEventTools.AddIntPersistentListener(b.onClick,p.SelectSkill,i);p.skillButtons[i]=b;
                var text=b.GetComponentInChildren<Text>();text.rectTransform.anchoredPosition=new Vector2(23,0);text.rectTransform.sizeDelta=new Vector2(203,73);text.fontSize=20;
                var icon=Panel("秘籍图标",b.transform,-105,0,35,35,Color.white);icon.preserveAspect=true;p.skillIcons[i]=icon;
                view.skillHints[i]=Label("可用条件",bottom.transform,"",x,-44,267,24,16,Muted,TextAnchor.MiddleCenter);
            }
            p.basicButton=Button("吐纳掌",bottom.transform,"吐纳掌\n无需运笔 · 6 伤害",284,8,255,70);UnityEventTools.AddPersistentListener(p.basicButton.onClick,p.Basic);
            p.restButton=Button("调息",bottom.transform,"调息\n恢复 2 内力",569,8,255,70);UnityEventTools.AddPersistentListener(p.restButton.onClick,p.Rest);
            Label("基础动作说明",bottom.transform,"稳住内力，把握下一次破绽",425,-44,525,24,16,Muted,TextAnchor.MiddleCenter);
            var draw=Panel("经脉施法面板",root.transform,480,-41,560,544,Ink,true);p.drawingPanel=draw.gameObject;
            view.selectionLabel=Label("当前秘籍与战术",draw.transform,"",0,232,514,64,18);
            var boardObject=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/GestureBoard.prefab"),draw.transform);
            p.board=boardObject.GetComponent<GestureBoard>();var boardRect=(RectTransform)boardObject.transform;boardRect.anchorMin=boardRect.anchorMax=boardRect.pivot=new Vector2(.5f,.5f);boardRect.anchoredPosition=new Vector2(0,39);boardRect.sizeDelta=Vector2.one*290;
            var boardFeedback=p.board.GetComponent<GestureBoardFeedback>();
            if(boardFeedback==null)boardFeedback=PracticePolishSetup.Feedback(p.board,null);
            view.gradeLabel=Label("评级与实际倍率",draw.transform,"静心 · 起笔",0,-141,510,28,22,Gold,TextAnchor.MiddleCenter);
            view.breakdownLabel=Label("评分分项",draw.transform,"贴合 60% · 顺序 25% · 长度 15%",0,-167,530,24,15,Muted,TextAnchor.MiddleCenter);
            view.recoveryLabel=Label("本笔提示",draw.transform,"从 1 号穴位起笔",0,-205,530,48,16,null,TextAnchor.MiddleCenter);
            // Live next-node guidance owns a separate line; scored explanations are owned by the HUD view.
            boardFeedback.hint=Label("运笔过程提示",draw.transform,"",0,-116,510,18,13,Muted,TextAnchor.MiddleCenter);
            p.confirmButton=Button("确认施放",draw.transform,"确认施放",130,-251,242,40);UnityEventTools.AddPersistentListener(p.confirmButton.onClick,p.Confirm);
            var clear=Button("清空重画",draw.transform,"重画这一笔",-130,-251,242,40);UnityEventTools.AddPersistentListener(clear.onClick,p.Clear);
            p.scoreLabel=Label("兼容评分文本",draw.transform,"",0,0,0,0,16);p.scoreLabel.gameObject.SetActive(false);
            var result=Panel("胜败结算",root.transform,120,0,760,340,Ink,true);p.resultPanel=result.gameObject;
            Label("结算题签",result.transform,"此 役 已 定",0,135,650,38,22,Gold,TextAnchor.MiddleCenter);
            p.resultLabel=Label("胜败",result.transform,"",0,45,680,110,34,null,TextAnchor.MiddleCenter);
            Label("结果说明",result.transform,"可再试另一套构筑；联机再战需双方同意。",0,-35,680,45,18,Muted,TextAnchor.MiddleCenter);
            var restart=Button("再战",result.transform,"再战一场",-177,-108,308,56);UnityEventTools.AddPersistentListener(restart.onClick,p.Restart);
            var back=Button("返回",result.transform,"返回归云谷",177,-108,308,56);UnityEventTools.AddPersistentListener(back.onClick,p.ReturnToPractice);
            var net=root.AddComponent<NetworkBattlePanel>();net.battle=p;
            var netPanel=Panel("联机操作",root.transform,15,177,720,84,Ink,true);net.panel=netPanel.gameObject;
            net.status=Label("连接状态",netPanel.transform,"",0,21,720,30,18,null,TextAnchor.MiddleCenter);
            net.rematch=Button("双方再战",netPanel.transform,"再战一局",-240,-19,220,34);UnityEventTools.AddPersistentListener(net.rematch.onClick,net.Rematch);
            net.room=Button("返回房间",netPanel.transform,"回房间换构筑",0,-19,220,34);UnityEventTools.AddPersistentListener(net.room.onClick,net.Room);
            net.recover=Button("恢复连接",netPanel.transform,"恢复连接",240,-19,220,34);UnityEventTools.AddPersistentListener(net.recover.onClick,net.Recover);
            var compat=Rect("兼容反馈文本",root.transform,0,0,0,0);f.intention=Label("旧意图",compat,"",0,0,0,0);f.description=Label("旧技能说明",compat,"",0,0,0,0);compat.gameObject.SetActive(false);
            f.floating=new[]{Label("席位一跳字",root.transform,"",-210,0,280,70,30,Gold,TextAnchor.MiddleCenter),Label("席位二跳字",root.transform,"",175,0,280,70,30,Gold,TextAnchor.MiddleCenter)};
            // Small sourced icons are visual keys, not additional status authorities.
            Icon("护盾图例",strategy.transform,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/StageC/UI/Shield.png"),158,53,23,Paper);
            Icon("灼伤图例",record.transform,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/StageC/UI/Burn.png"),158,128,23,Paper);
            var fontBinding=root.AddComponent<SavedChineseFont>();fontBinding.profile=fonts;fontBinding.Apply();
            draw.gameObject.SetActive(false);result.gameObject.SetActive(false);netPanel.gameObject.SetActive(false);
        }

        static void SavePractice()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/GestureLab.unity");var lab=UnityEngine.Object.FindObjectOfType<GestureLabPresenter>();
            if(PrefabUtility.IsPartOfPrefabInstance(lab.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(lab.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var legacy=lab.GetComponent<LocalChineseFont>();if(legacy!=null)UnityEngine.Object.DestroyImmediate(legacy);
            var binding=lab.GetComponent<SavedChineseFont>();if(binding==null)binding=lab.gameObject.AddComponent<SavedChineseFont>();binding.profile=fonts;
            foreach(var panelName in new[]{"秘籍选择面板","评分面板"}){var panel=lab.transform.Find(panelName);if(panel!=null){var image=panel.GetComponent<Image>();image.sprite=null;image.color=Ink;}}
            foreach(var b in lab.GetComponentsInChildren<Button>(true)){b.image.sprite=buttonSkin;b.image.type=Image.Type.Sliced;var colors=b.colors;colors.highlightedColor=new Color(1,.91f,.68f);colors.pressedColor=new Color(.61f,.85f,.86f);b.colors=colors;}
            foreach(var t in lab.GetComponentsInChildren<Text>(true))t.color=Paper;
            lab.routeTitle.color=Gold;lab.gradeText.color=Gold;binding.Apply();
            PrefabUtility.SaveAsPrefabAssetAndConnect(lab.gameObject,"Assets/_Game/Prefabs/GestureLabUI.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("一笔江湖/3.0/阶段C/构建战斗验证版")]
        public static void Build()
        {
            PolishEnvironmentSetup.Guard();
            foreach(var guid in AssetDatabase.FindAssets("t:BattlePresentationProfile",new[]{Content})){
                var profile=AssetDatabase.LoadAssetAtPath<BattlePresentationProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if(profile.ValidateConfiguration().Length!=0)throw new InvalidOperationException("战斗表现配置无效："+profile.name);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:NpcTacticProfile",new[]{Content})){
                var profile=AssetDatabase.LoadAssetAtPath<NpcTacticProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if(profile.ValidateConfiguration().Length!=0)throw new InvalidOperationException("敌人套路配置无效："+profile.name);
            }
            ContentExporter.Export();
            string root=Directory.GetParent(Application.dataPath).Parent.FullName;string output=Path.Combine(root,"Builds/FrameworkStageC");Directory.CreateDirectory(output);
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,Path.Combine(output,"YibiJianghu.exe"),BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText(Path.Combine(root,"Docs/Evidence/FrameworkStageC-build.txt"),"utc="+DateTime.UtcNow.ToString("o")+"\nresult="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nwarnings="+report.summary.totalWarnings+"\nbytes="+report.summary.totalSize+"\nseconds="+report.summary.totalTime.TotalSeconds);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Stage C build failed.");
        }
    }

    [CustomEditor(typeof(BattlePresentationProfile))]
    sealed class BattlePresentationProfileEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();foreach(var error in ((BattlePresentationProfile)target).ValidateConfiguration())EditorGUILayout.HelpBox(error,MessageType.Error);EditorGUILayout.HelpBox("这里只配置说明与表现节奏；伤害、内力和评分仍由共同规则决定。",MessageType.Info);}
    }
    [CustomEditor(typeof(NpcTacticProfile))]
    sealed class NpcTacticProfileEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();foreach(var error in ((NpcTacticProfile)target).ValidateConfiguration())EditorGUILayout.HelpBox(error,MessageType.Error);}
    }
}
