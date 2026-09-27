using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using Yibi.Battle;
using Yibi.World;
using Yibi.UI;
using Yibi.Presentation;

namespace Yibi.Editor
{
    public static class AdventureSetup
    {
        const string Art="Assets/_Game/Art/Selected/";
        static Font font;
        static Sprite panel,button;
        static void Init(){font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);panel=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"UI/武侠蓝纹面板.asset");button=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"UI/青玉按钮.asset");}
        static Image Panel(string name,Transform parent,Vector2 pos,Vector2 size){var image=ArenaSetup.Panel(name,parent,pos,size,Color.white);image.sprite=panel;image.type=Image.Type.Sliced;return image;}
        static Text Text(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize){var label=ArenaSetup.Label(name,parent,value,pos,size,fontSize);label.font=font;return label;}
        static Button Button(string name,Transform parent,string value,Vector2 pos,Vector2 size){var b=ArenaSetup.Button(name,parent,value,pos,size);b.image.sprite=button;b.image.type=Image.Type.Sliced;b.image.color=Color.white;b.GetComponentInChildren<Text>().font=font;return b;}
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before saving assets");Init();
            foreach(var path in new[]{"Assets/_Game/Prefabs/PlayerExplorer.prefab","Assets/_Game/Prefabs/BattleActor.prefab"}){var root=PrefabUtility.LoadPrefabContents(path);RigModels(root);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);}
            foreach(var name in new[]{"Valley","Arena_Stone","Arena_Bamboo","GestureLab"}){
                var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");foreach(var root in scene.GetRootGameObjects())RigModels(root);
                if(name=="Valley"&&UnityEngine.Object.FindObjectOfType<JourneyPresenter>()==null)Valley();
                if(name.StartsWith("Arena")&&UnityEngine.Object.FindObjectOfType<BattleFeedback>()==null)Battle();
                if(name=="GestureLab"&&UnityEngine.Object.FindObjectOfType<PracticeProgress>()==null){var lab=UnityEngine.Object.FindObjectOfType<GestureLabPresenter>();var progress=lab.gameObject.AddComponent<PracticeProgress>();progress.board=lab.board;progress.objective=Text("归云篇练功目标",lab.transform,"亲手绘制一条路线达到 70 分",new Vector2(0,310),new Vector2(950,45),19);progress.objective.alignment=TextAnchor.MiddleCenter;}
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");
        }
        static void RigModels(GameObject root)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
                if(filter.sharedMesh==null||!CharacterRigBuilder.Models.Contains(filter.sharedMesh.name))continue;
                if(filter.GetComponentInChildren<CharacterMotion>(true)!=null)continue;
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer!=null){renderer.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRigBuilder.Folder+"/"+filter.sharedMesh.name+".prefab");var actor=(GameObject)PrefabUtility.InstantiatePrefab(source);actor.transform.SetParent(filter.transform,false);actor.name="骨骼角色";
            }
        }
        static void Valley()
        {
            var p=UnityEngine.Object.FindObjectOfType<ValleyPresenter>();var j=p.gameObject.AddComponent<JourneyPresenter>();j.valley=p;p.journey=j;PrefabUtility.RecordPrefabInstancePropertyModifications(p);
            var e=p.explorer;e.speed=3.5f;e.sprintSpeed=6;e.motion=e.GetComponentInChildren<CharacterMotion>();e.respawnPoint=new Vector3(0,1,-14);PrefabUtility.RecordPrefabInstancePropertyModifications(e);
            var quest=Panel("归云篇任务",p.transform,new Vector2(-420,200),new Vector2(390,120));j.chapter=Text("章节",quest.transform,"",new Vector2(0,30),new Vector2(340,30),22);j.objective=Text("目标",quest.transform,"",new Vector2(0,-8),new Vector2(340,48),17);j.compass=Text("方向距离",quest.transform,"",new Vector2(0,-42),new Vector2(340,25),16);
            var map=Panel("归云谷导览",p.transform,new Vector2(515,176),new Vector2(200,175));
            Text("北向",map.transform,"北 ↑   归云谷",new Vector2(0,65),new Vector2(160,25),16);j.mapTargets=new RectTransform[5];
            for(int i=0;i<5;i++){var marker=ArenaSetup.Panel("交互地标_"+i,map.transform,Vector2.zero,Vector2.one*10,new Color(1,.72f,.27f));marker.raycastTarget=false;marker.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");j.mapTargets[i]=marker.rectTransform;}
            var player=ArenaSetup.Panel("玩家位置",map.transform,Vector2.zero,Vector2.one*12,Color.cyan);player.raycastTarget=false;player.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");j.mapPlayer=player.rectTransform;
            j.collection=Text("收集记录",p.transform,"",new Vector2(470,65),new Vector2(300,32),16);j.treasureHint=Text("遗匣提示",p.transform,"",new Vector2(0,-212),new Vector2(700,35),23);j.treasureHint.alignment=TextAnchor.MiddleCenter;
            var dialog=Panel("江湖对话",p.transform,new Vector2(0,-115),new Vector2(880,300));j.dialoguePanel=dialog.gameObject;j.dialogueTitle=Text("说话人",dialog.transform,"",new Vector2(0,105),new Vector2(800,45),26);j.dialogueBody=Text("对话内容",dialog.transform,"",new Vector2(0,15),new Vector2(800,145),21);
            j.accept=Button("接受",dialog.transform,"开始",new Vector2(210,-103),new Vector2(260,48));UnityEventTools.AddPersistentListener(j.accept.onClick,j.AcceptDialogue);var cancel=Button("暂别",dialog.transform,"稍后再来",new Vector2(-210,-103),new Vector2(260,48));UnityEventTools.AddPersistentListener(cancel.onClick,j.CloseDialogue);dialog.gameObject.SetActive(false);
            var popups=new[]{p.inventoryPanel,j.dialoguePanel,p.pausePanel};for(int i=0;i<popups.Length;i++){var layer=popups[i].GetComponent<ModalLayer>();if(layer==null)layer=popups[i].AddComponent<ModalLayer>();layer.order=20+i*10;}
            // Extend the existing bag into a visible collection and explicit loadout editor.
            var inv=p.inventoryPanel.GetComponent<RectTransform>();inv.sizeDelta=new Vector2(1000,530);p.profileText.rectTransform.anchoredPosition=new Vector2(0,168);p.profileText.rectTransform.sizeDelta=new Vector2(900,110);p.profileText.fontSize=18;
            j.skillCards=new Image[6];for(int i=0;i<6;i++){var card=Button("秘籍卡_"+i,inv,Yibi.Rules.BattleContent.Names[i],new Vector2(-395+i*158,70),new Vector2(145,55));card.GetComponentInChildren<Text>().fontSize=19;UnityEventTools.AddIntPersistentListener(card.onClick,j.SelectTechnique,i);j.skillCards[i]=card.image;}
            j.techniqueDetail=Text("秘籍说明",inv,"",new Vector2(0,-3),new Vector2(900,95),19);
            j.equipSelected=new Button[3];for(int i=0;i<3;i++){p.equipButtons[i].gameObject.SetActive(false);var b=Button("明确装配槽_"+i,inv,"装入第 "+(i+1)+" 槽",new Vector2(-300+i*300,-85),new Vector2(270,45));UnityEventTools.AddIntPersistentListener(b.onClick,j.EquipTechnique,i);j.equipSelected[i]=b;}
            foreach(var text in inv.GetComponentsInChildren<Text>(true))if(text.transform.parent.name=="心法"||text.transform.parent.name=="免费抽签")((RectTransform)text.transform.parent).anchoredPosition+=new Vector2(0,-35);
            var close=inv.Find("关闭");if(close!=null)((RectTransform)close).anchoredPosition=new Vector2(0,-225);
            var props=new GameObject("探索内容_遗匣与路标");var chest=ChestPrefab();j.caches=new TreasureCache[3];var positions=new[]{new Vector3(-10,.15f,8),new Vector3(52,.15f,12),new Vector3(15,.15f,60)};
            for(int i=0;i<3;i++){var g=(GameObject)PrefabUtility.InstantiatePrefab(chest);g.transform.SetParent(props.transform);g.transform.position=positions[i];g.name="遗匣_"+(i+1);var c=g.AddComponent<TreasureCache>();c.stableId="guiyun-"+(i+1);c.tickets=1;j.caches[i]=c;var marker=new GameObject("未领取标记",typeof(TextMesh),typeof(WorldSign));marker.transform.SetParent(g.transform,false);marker.transform.localPosition=new Vector3(0,2,0);var label=marker.GetComponent<TextMesh>();label.text="江湖遗匣";label.characterSize=.18f;label.fontSize=32;label.color=new Color(1,.8f,.25f);label.anchor=TextAnchor.MiddleCenter;c.glow=marker;}
            // Restore navigation space: trees are scenery; stone platforms and buildings retain collision.
            var bounds=new GameObject("探索边界_碰撞");bounds.transform.SetParent(props.transform);Box(bounds.transform,"西界",new Vector3(-28,2,30),new Vector3(1,8,130));Box(bounds.transform,"东界",new Vector3(66,2,30),new Vector3(1,8,130));Box(bounds.transform,"南界",new Vector3(19,2,-34),new Vector3(95,8,1));Box(bounds.transform,"北界",new Vector3(19,2,94),new Vector3(95,8,1));
            // NPC at the practice platform makes the conversation visible in the world.
            var teacher=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRigBuilder.Folder+"/行者_青衫.prefab"));teacher.name="听松先生";teacher.transform.SetParent(props.transform);teacher.transform.position=new Vector3(-5,1.2f,-10);teacher.transform.rotation=Quaternion.Euler(0,180,0);
            foreach(var label in p.GetComponentsInChildren<Text>(true)){PrefabUtility.RecordPrefabInstancePropertyModifications(label);PrefabUtility.RecordPrefabInstancePropertyModifications(label.rectTransform);}
            foreach(var rect in inv.GetComponentsInChildren<RectTransform>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            EditorUtility.SetDirty(p);EditorUtility.SetDirty(j);
        }
        static void Box(Transform root,string name,Vector3 center,Vector3 size){var g=new GameObject(name,typeof(BoxCollider));g.transform.SetParent(root);g.transform.position=center;g.GetComponent<BoxCollider>().size=size;}
        static GameObject ChestPrefab()
        {
            string path=Art+"江湖遗匣.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/场景模型/Assets/FBX/49-50.FBX"));var f=source.GetComponentsInChildren<MeshFilter>().First(x=>x.name=="Item_baoxiang_001");var mesh=new Mesh{name="江湖遗匣"};mesh.CombineMeshes(Enumerable.Range(0,f.sharedMesh.subMeshCount).Select(i=>new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=f.transform.localToWorldMatrix}).ToArray(),false,true);var b=mesh.bounds;var v=mesh.vertices;float scale=1.15f/b.size.y;for(int i=0;i<v.Length;i++)v[i]=(v[i]-new Vector3(b.center.x,b.min.y,b.center.z))*scale;mesh.vertices=v;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Art+"江湖遗匣.asset");
            var materials=f.GetComponent<MeshRenderer>().sharedMaterials.Select((m,i)=>{var copy=new Material(Shader.Find("Universal Render Pipeline/Lit"));copy.mainTexture=m.mainTexture;copy.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(copy,Art+"江湖遗匣_"+i+".mat");return copy;}).ToArray();var root=new GameObject("江湖遗匣",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterials=materials;root.GetComponent<BoxCollider>().center=mesh.bounds.center;root.GetComponent<BoxCollider>().size=mesh.bounds.size;var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(source);return prefab;
        }
        static void Battle()
        {
            var p=UnityEngine.Object.FindObjectOfType<BattlePresenter>();var f=p.gameObject.AddComponent<BattleFeedback>();f.battle=p;p.feedback=f;PrefabUtility.RecordPrefabInstancePropertyModifications(p);
            f.healthFills=new Image[2];f.energyFills=new Image[2];f.floating=new Text[2];
            for(int i=0;i<2;i++){
                float x=i==0?-385:385;var back=ArenaSetup.Panel("生命条底_"+i,p.transform,new Vector2(x,217),new Vector2(420,12),new Color(.08f,.12f,.15f));back.raycastTarget=false;
                f.healthFills[i]=ArenaSetup.Panel("生命条_"+i,back.transform,Vector2.zero,new Vector2(420,12),i==0?new Color(.23f,.78f,.56f):new Color(.86f,.32f,.28f));var r=f.healthFills[i].rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);r.anchoredPosition=Vector2.zero;f.healthFills[i].raycastTarget=false;
                var energy=ArenaSetup.Panel("内力条底_"+i,p.transform,new Vector2(x,200),new Vector2(420,7),new Color(.08f,.12f,.15f));energy.raycastTarget=false;f.energyFills[i]=ArenaSetup.Panel("内力条_"+i,energy.transform,Vector2.zero,new Vector2(420,7),new Color(.24f,.68f,.94f));r=f.energyFills[i].rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);r.anchoredPosition=Vector2.zero;f.energyFills[i].raycastTarget=false;
                f.floating[i]=Text("战斗跳字_"+i,p.transform,"",new Vector2(i==0?-155:170,-15),new Vector2(240,60),30);f.floating[i].alignment=TextAnchor.MiddleCenter;
            }
            p.turnLabel.rectTransform.anchoredPosition=new Vector2(180,165);p.turnLabel.rectTransform.sizeDelta=new Vector2(650,35);p.turnLabel.fontSize=20;PrefabUtility.RecordPrefabInstancePropertyModifications(p.turnLabel.rectTransform);PrefabUtility.RecordPrefabInstancePropertyModifications(p.turnLabel);
            f.intention=Text("战术提示",p.transform,"",new Vector2(0,112),new Vector2(800,52),18);f.intention.alignment=TextAnchor.MiddleCenter;
            f.description=Text("秘籍战术说明",p.transform,"",new Vector2(0,-211),new Vector2(1180,38),17);f.description.alignment=TextAnchor.MiddleCenter;
            var leave=p.transform.Find("离开擂台");var resume=p.transform.Find("恢复连接");if(leave!=null)((RectTransform)leave).anchoredPosition=new Vector2(-520,155);if(resume!=null)((RectTransform)resume).anchoredPosition=new Vector2(-320,155);
            p.leftActor.rotation=Quaternion.Euler(0,0,0);p.rightActor.rotation=Quaternion.Euler(0,0,0);
            foreach(var actor in new[]{p.leftActor,p.rightActor}){var motion=actor.GetComponentInChildren<CharacterMotion>();if(motion!=null){var visual=motion.transform.parent;visual.localRotation=Quaternion.Euler(0,actor==p.leftActor?90:270,0);PrefabUtility.RecordPrefabInstancePropertyModifications(visual);}}
            EditorUtility.SetDirty(p);EditorUtility.SetDirty(f);
        }
    }
}
