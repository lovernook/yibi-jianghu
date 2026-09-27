using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using Yibi.UI;
using Yibi.World;
using Yibi.Rules;

namespace Yibi.Editor
{
    public static class PracticePolishSetup
    {
        static Font font;
        static Sprite buttonSkin;
        static void Place(RectTransform rect,Vector2 p,Vector2 size){rect.anchoredPosition=p;rect.sizeDelta=size;}
        static Text Label(string name,Transform parent,string value,Vector2 p,Vector2 size,int fontSize=18){var t=ArenaSetup.Label(name,parent,value,p,size,fontSize);t.font=font;t.raycastTarget=false;return t;}
        static Button Button(string name,Transform parent,string value,Vector2 p,Vector2 size){var b=ArenaSetup.Button(name,parent,value,p,size);b.image.sprite=buttonSkin;b.image.type=Image.Type.Sliced;b.image.color=Color.white;b.GetComponentInChildren<Text>().font=font;return b;}
        static RectTransform Stretch(string name,Transform parent){var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;return t;}
        public static GestureBoardFeedback Feedback(GestureBoard board,Text hint)
        {
            var feedback=board.GetComponent<GestureBoardFeedback>();if(feedback!=null)return feedback;
            feedback=board.gameObject.AddComponent<GestureBoardFeedback>();feedback.board=board;feedback.hint=hint;
            feedback.rings=board.nodeMarkers.Select(n=>n.GetComponent<Graphic>()).ToArray();
            var line=Stretch("示范与回放轨迹",board.transform);feedback.guideLine=line.gameObject.AddComponent<PolylineGraphic>();feedback.guideLine.thickness=5;feedback.guideLine.color=new Color(.96f,.78f,.35f);feedback.guideLine.raycastTarget=false;
            var brush=new GameObject("笔锋光点",typeof(RectTransform),typeof(MeridianNodeGraphic));brush.transform.SetParent(board.transform,false);feedback.brush=brush.GetComponent<RectTransform>();feedback.brush.sizeDelta=Vector2.one*18;var graphic=brush.GetComponent<MeridianNodeGraphic>();graphic.color=new Color(1,.95f,.7f);graphic.raycastTarget=false;brush.SetActive(false);
            return feedback;
        }
        [MenuItem("一笔江湖/玩法/安装六脉练功与引导（仅首次）")]
        public static void Install()
        {
            PolishEnvironmentSetup.Guard();var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/GestureLab.unity");var lab=Object.FindObjectOfType<GestureLabPresenter>();
            if(lab.GetComponent<PracticeSession>()!=null)throw new System.InvalidOperationException("已安装，请直接编辑保存的 UI");
            if(PrefabUtility.IsPartOfPrefabInstance(lab.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(lab.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);buttonSkin=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/UI/青玉按钮.asset");
            var session=lab.gameObject.AddComponent<PracticeSession>();session.lab=lab;session.routeButtons=new Button[6];lab.routes=new GestureTemplateSO[6];lab.routeHighlights=new Image[6];
            var left=lab.transform.Find("秘籍选择面板");
            for(int i=0;i<6;i++){
                lab.routes[i]=AssetDatabase.LoadAssetAtPath<GestureTemplateSO>("Assets/_Game/Data/Gestures/"+BattleContent.Ids[i]+".asset");
                var old=left.Find("路线_"+BattleContent.Names[i]);var b=old==null?Button("路线_"+BattleContent.Names[i],left,"",Vector2.zero,new Vector2(232,45)):old.GetComponent<Button>();
                Place(b.GetComponent<RectTransform>(),new Vector2(0,224-i*53),new Vector2(232,45));b.GetComponentInChildren<Text>().text="0"+(i+1)+"  "+BattleContent.Names[i];b.GetComponentInChildren<Text>().fontSize=19;
                if(old==null)UnityEventTools.AddIntPersistentListener(b.onClick,lab.SelectRoute,i);session.routeButtons[i]=b;lab.routeHighlights[i]=b.image;
            }
            Place(lab.routeDescription.rectTransform,new Vector2(0,-105),new Vector2(232,66));lab.routeDescription.fontSize=17;
            Place(left.Find("清空重画").GetComponent<RectTransform>(),new Vector2(0,-174),new Vector2(232,42));
            Place(left.Find("固定样本").GetComponent<RectTransform>(),new Vector2(-61,-276),new Vector2(112,42));left.Find("固定样本").GetComponentInChildren<Text>().text="测试样本";left.Find("固定样本").GetComponentInChildren<Text>().fontSize=16;
            Place(left.Find("保存手绘").GetComponent<RectTransform>(),new Vector2(61,-276),new Vector2(112,42));left.Find("保存手绘").GetComponentInChildren<Text>().text="保存笔迹";left.Find("保存手绘").GetComponentInChildren<Text>().fontSize=16;
            var feedback=Feedback(lab.board,lab.statusText);
            var demo=Button("运笔示范",left,"运笔示范",new Vector2(-61,-225),new Vector2(112,42));demo.GetComponentInChildren<Text>().fontSize=17;UnityEventTools.AddPersistentListener(demo.onClick,feedback.Demonstrate);
            var replay=Button("笔迹回放",left,"回看本笔",new Vector2(61,-225),new Vector2(112,42));replay.GetComponentInChildren<Text>().fontSize=17;UnityEventTools.AddPersistentListener(replay.onClick,feedback.Replay);
            // Preserve diagnostic detail behind an explicit toggle; the default explanation is for players.
            var right=lab.transform.Find("评分面板");Place(lab.detailsText.rectTransform,new Vector2(0,-146),new Vector2(318,218));lab.detailsText.fontSize=17;
            var detail=Button("评分算法详情",right,"查看评分细节",new Vector2(0,-292),new Vector2(310,40));detail.GetComponentInChildren<Text>().fontSize=17;UnityEventTools.AddPersistentListener(detail.onClick,lab.ToggleDiagnostics);
            session.history=Object.Instantiate(lab.sampleText,lab.transform);session.history.name="每脉练习记录";lab.sampleText.gameObject.SetActive(false);Place(session.history.rectTransform,new Vector2(-65,-357),new Vector2(620,52));session.history.fontSize=17;
            session.examStatus=Label("六脉考核进度",lab.transform,"自由练功 · 六脉均可试学；六项 80 分可完成考核",new Vector2(-68,354),new Vector2(610,34),18);session.examStatus.alignment=TextAnchor.MiddleCenter;
            session.examButton=Button("六脉考核",lab.transform,"六脉考核",new Vector2(-216,-399),new Vector2(280,42));UnityEventTools.AddPersistentListener(session.examButton.onClick,session.ToggleExam);
            session.nextButton=Button("考核下一题",lab.transform,"下一题",new Vector2(90,-399),new Vector2(280,42));UnityEventTools.AddPersistentListener(session.nextButton.onClick,session.Next);
            Place(lab.statusText.rectTransform,new Vector2(-65,-326),new Vector2(620,28));lab.statusText.fontSize=16;
            lab.transform.Find("阶段说明").GetComponent<Text>().text="归云练功院 · 六脉研习";
            var back=lab.transform.Find("返回归云谷").GetComponent<RectTransform>();Place(back,new Vector2(526,405),new Vector2(290,42));
            var objective=lab.GetComponent<PracticeProgress>().objective;Place(objective.rectTransform,new Vector2(-60,389),new Vector2(570,28));objective.fontSize=16;
            foreach(var t in lab.GetComponentsInChildren<Text>(true))t.font=font;
            PrefabUtility.SaveAsPrefabAsset(lab.gameObject,"Assets/_Game/Prefabs/GestureLabUI.prefab");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            foreach(var name in new[]{"Arena_Stone","Arena_Bamboo"}){
                scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");foreach(var board in Object.FindObjectsOfType<GestureBoard>(true))Feedback(board,null);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            var prefab=PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/GestureBoard.prefab");Feedback(prefab.GetComponent<GestureBoard>(),null);PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/_Game/Prefabs/GestureBoard.prefab");PrefabUtility.UnloadPrefabContents(prefab);
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/GestureLab.unity");
        }
        public static void RepairSavedUI()
        {
            PolishEnvironmentSetup.Guard();var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/GestureLab.unity");var lab=Object.FindObjectOfType<GestureLabPresenter>();
            if(PrefabUtility.IsPartOfPrefabInstance(lab.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(lab.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var replacements=new System.Collections.Generic.Dictionary<Object,Object>();
            foreach(var t in lab.GetComponentsInChildren<Transform>(true))foreach(var group in t.GetComponents<MonoBehaviour>().Where(c=>c!=null&&c.GetType().Namespace!=null&&c.GetType().Namespace.StartsWith("Yibi.")).GroupBy(c=>c.GetType())){
                var array=group.ToArray();for(int i=0;i<array.Length-1;i++)replacements[array[i]]=array[array.Length-1];
            }
            var duplicateObjects=new System.Collections.Generic.List<GameObject>();
            foreach(var parent in lab.GetComponentsInChildren<Transform>(true))foreach(var group in parent.Cast<Transform>().GroupBy(t=>t.name).Where(g=>g.Count()>1)){
                var array=group.ToArray();for(int i=0;i<array.Length-1;i++){MapTree(array[i],array[array.Length-1],replacements);duplicateObjects.Add(array[i].gameObject);}
            }
            // Only rewire script/UI fields. Never rewrite Transform ownership or native hierarchy arrays.
            foreach(var component in lab.GetComponentsInChildren<MonoBehaviour>(true)){
                if(component==null||duplicateObjects.Any(g=>component.transform.IsChildOf(g.transform)))continue;var serialized=new SerializedObject(component);var property=serialized.GetIterator();
                while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue!=null&&property.name!="m_GameObject"&&property.name!="m_PrefabInstance"&&property.name!="m_CorrespondingSourceObject"){Object target;if(replacements.TryGetValue(property.objectReferenceValue,out target))property.objectReferenceValue=target;}
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(var duplicate in replacements.Keys.OfType<MonoBehaviour>().ToArray())if(duplicate!=null&&!duplicateObjects.Any(g=>duplicate.transform.IsChildOf(g.transform)))Object.DestroyImmediate(duplicate);
            foreach(var duplicate in duplicateObjects)if(duplicate!=null)Object.DestroyImmediate(duplicate);
            var session=lab.GetComponent<PracticeSession>();
            if(session.history==lab.sampleText){
                session.history=Object.Instantiate(lab.sampleText,lab.transform);session.history.name="每脉练习记录";
                lab.sampleText.gameObject.SetActive(false);
            }
            session.history.text="此经脉尚无手绘记录";
            PrefabUtility.SaveAsPrefabAssetAndConnect(lab.gameObject,"Assets/_Game/Prefabs/GestureLabUI.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static void MapTree(Transform old,Transform keep,System.Collections.Generic.Dictionary<Object,Object> map)
        {
            map[old.gameObject]=keep.gameObject;
            foreach(var c in old.GetComponents<Component>())if(c!=null){var target=keep.GetComponent(c.GetType());if(target!=null)map[c]=target;}
            foreach(Transform child in old){var target=keep.Find(child.name);if(target!=null)MapTree(child,target,map);}
        }
    }
}
