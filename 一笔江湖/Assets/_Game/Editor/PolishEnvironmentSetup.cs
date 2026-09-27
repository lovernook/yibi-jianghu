using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Yibi.World;
using Yibi.Presentation;

namespace Yibi.Editor
{
    public static class PolishEnvironmentSetup
    {
        const string Characters="Assets/_Game/Art/Selected/Characters/";
        const string Selected="Assets/_Game/Art/Selected/";
        public static readonly string[] Residents={"行脚客","布衣药师","白发隐士","谷中书生","练拳弟子","归云村民"};
        static readonly string[] Suffixes={"017","018","020","021","022","024"};
        static readonly string[] Textures={"npc_pt008_mip_0.png","npc_pt007_mip_0.png","npc_pt004.png","npc_pt003.png","npc_pt002.png","npc_pt001.png"};
        public static GameObject Spawn(string name,Transform parent,Vector3 position,float scale=1,float yaw=0)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Characters+name+".prefab");
            if(asset==null)throw new InvalidOperationException("缺少资源 "+name);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(asset);g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localRotation=Quaternion.Euler(0,yaw,0);g.transform.localScale=Vector3.one*scale;return g;
        }
        [MenuItem("一笔江湖/画面/准备新增资源与预览")]
        public static void PrepareGallery()
        {
            Guard();
            var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Characters+"CharacterCollection.fbx"));
            var extract=typeof(ImportedModelSetup).GetMethod("Extract",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            try{for(int i=0;i<Residents.Length;i++)extract.Invoke(null,new object[]{source,Suffixes[i],Residents[i],Textures[i],2.6f});}
            finally{UnityEngine.Object.DestroyImmediate(source);}
            var prop=typeof(ImportedModelSetup).GetMethod("Prop",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            prop.Invoke(null,new object[]{"07-10","Item_dengzhu","石灯柱",3f,false});
            prop.Invoke(null,new object[]{"07-10","Item_greenfields_1001_bridge01","归云石桥",3f,false});
            prop.Invoke(null,new object[]{"27-30","Item_ludeng","庭院路灯",3f,false});
            prop.Invoke(null,new object[]{"23-26","Item_desert_2001_weilan01","木围栏",1.3f,false});
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            for(int i=0;i<Residents.Length;i++){Spawn(Residents[i],null,new Vector3((i-2.5f)*2.7f,0,0),1,180);}
            for(int i=0;i<4;i++)Spawn(new[]{"石灯柱","归云石桥","庭院路灯","木围栏"}[i],null,new Vector3((i-1.5f)*5,0,7));
            Camera.main.transform.position=new Vector3(0,7,-18);Camera.main.transform.LookAt(new Vector3(0,1.3f,2));
            Lighting();ArenaSetup.Shape("预览地面",PrimitiveType.Cube,null,new Vector3(0,-.2f,3),new Vector3(34,.4f,20),AssetDatabase.LoadAssetAtPath<Material>(Selected+"资源_擂台地砖.mat"));
            EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/PolishAssetGallery.unity");AssetDatabase.SaveAssets();
        }
        public static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play");if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("请先保存当前场景");}
        public static void Lighting()
        {
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Selected+"归云晴空.mat");
            if(sky==null){sky=new Material(Shader.Find("Yibi/Guiyun Sky")){name="归云晴空"};AssetDatabase.CreateAsset(sky,Selected+"归云晴空.mat");}
            RenderSettings.skybox=sky;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.6f,.73f,.84f);RenderSettings.ambientEquatorColor=new Color(.57f,.63f,.61f);RenderSettings.ambientGroundColor=new Color(.32f,.35f,.31f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.64f,.76f,.78f);RenderSettings.fogStartDistance=75;RenderSettings.fogEndDistance=240;
            foreach(var camera in UnityEngine.Object.FindObjectsOfType<Camera>()){camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=600;}
            var light=UnityEngine.Object.FindObjectsOfType<Light>().FirstOrDefault(l=>l.type==LightType.Directional&&l.name!="柔和补光");
            if(light!=null){light.color=new Color(1,.91f,.77f);light.intensity=1.15f;light.shadowStrength=.48f;light.transform.rotation=Quaternion.Euler(42,-28,0);RenderSettings.sun=light;}
        }
        public static void ApplySky()
        {
            Guard();foreach(var name in new[]{"MainMenu","Valley","Arena_Stone","Arena_Bamboo","GestureLab","AnimationWorkshop"}){var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");Lighting();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
        }
        static void Mountains(Transform parent,Vector3 center,int count,float radius)
        {
            for(int i=0;i<count;i++){float angle=i*Mathf.PI*2/count;var g=Spawn("山石",parent,center+new Vector3(Mathf.Sin(angle)*radius,-15,Mathf.Cos(angle)*radius),1,i*57);g.name="云岭_"+i;g.transform.localScale=new Vector3(6+i%3*2,4+i%4*1.5f,5+i%3);}
        }
        static void Collider(GameObject g){var c=g.AddComponent<BoxCollider>();var bounds=g.GetComponent<MeshFilter>().sharedMesh.bounds;c.center=bounds.center;c.size=bounds.size;}
        static void Resident(string asset,string title,string words,Transform parent,Vector3 position,float yaw)
        {
            var profile=AssetDatabase.LoadAssetAtPath<RigDefinition>(CharacterRigBuilder.Folder+"/"+asset+"_骨骼.asset");
            if(profile==null){profile=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<RigDefinition>(CharacterRigBuilder.Folder+"/侠客_劲装_骨骼.asset"));profile.name=asset;profile.sourceMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Characters+asset+".asset");profile.material=AssetDatabase.LoadAssetAtPath<Material>(Characters+asset+".mat");profile.actionScale=.25f;AssetDatabase.CreateAsset(profile,CharacterRigBuilder.Folder+"/"+asset+"_骨骼.asset");CharacterRigBuilder.Build(profile,asset);}
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRigBuilder.Folder+"/"+asset+".prefab"));g.name=title;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var person=g.AddComponent<ValleyResident>();person.displayName=title;person.dialogue=words;
            var collision=g.AddComponent<CapsuleCollider>();collision.center=new Vector3(0,1.2f,0);collision.height=2.4f;collision.radius=.35f;
        }
        [MenuItem("一笔江湖/画面/布置归云晴空与村落（仅首次）")]
        public static void DressScenes()
        {
            Guard();ApplySky();
            foreach(var name in new[]{"MainMenu","Valley","Arena_Stone","Arena_Bamboo","GestureLab"}){
                var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");if(GameObject.Find("晴空归云_场景细化")!=null)continue;
                var root=new GameObject("晴空归云_场景细化");var t=root.transform;
                Mountains(t,name=="Valley"?new Vector3(15,0,25):Vector3.zero,name=="Valley"?18:12,name=="Valley"?145:115);
                var grass=AssetDatabase.LoadAssetAtPath<Material>(Selected+"资源_苔草地.mat");ArenaSetup.Shape("远景地表",PrimitiveType.Cube,t,new Vector3(0,-2,30),new Vector3(450,.4f,450),grass);
                if(name=="MainMenu"){
                    Spawn("民居丙",t,new Vector3(-11,-1.5f,21),1.4f,130);Spawn("洛阳牌坊",t,new Vector3(12,-1.5f,17),1.4f,150);
                    Spawn("庭院路灯",t,new Vector3(6,-1.35f,3),1.3f,-10);Spawn("石灯柱",t,new Vector3(-4,-1.3f,9),1.5f,15);
                    for(int i=0;i<4;i++)Spawn("木围栏",t,new Vector3(4+i*4,-1.3f,11),1.4f,0);
                    // Move the old tree away from the character silhouette; frame the scene at its edges.
                    var trees=scene.GetRootGameObjects().Where(g=>g.name=="桃树").ToArray();if(trees.Length>0)trees[0].transform.position=new Vector3(7,-1.4f,8);if(trees.Length>1)trees[1].transform.position=new Vector3(-6,-1.4f,11);
                }
                else if(name=="Valley"){
                    for(int i=0;i<8;i++){var lamp=Spawn(i%2==0?"庭院路灯":"石灯柱",t,new Vector3(i%2==0?-6:6,0,-23+(i/2)*19),1,i%2==0?90:-90);Collider(lamp);}
                    for(int i=0;i<8;i++){var fence=Spawn("木围栏",t,new Vector3(-22,0,-23+i*7),1.2f,90);Collider(fence);}
                    for(int i=0;i<9;i++)Spawn("山石",t,new Vector3(62+i%2*7,-1,12+i*8),.7f+i%3*.2f,i*63);
                    Resident("行脚客","金衣行客 · 岚音","山谷中藏着三处遗匣。靠近后按 F 即可打开。\n\n签令只在江湖历练中获得，用来抽取未拥有的秘籍。",t,new Vector3(-9,0,-20),140);
                    Resident("布衣药师","青衣药师 · 苏禾","清心诀能治疗，也能优先驱散灼伤。\n\n看到对手积蓄回风时，留些内力应对下一轮流火。",t,new Vector3(9,0,-3),220);
                    Resident("白发隐士","蓝衫游侠 · 陆远","先点穴，再裂石。破绽只会留到你的下一次行动结束。\n\n得分会微调威力，但构筑和内力安排才是制胜关键。",t,new Vector3(29,0,7),180);
                    Resident("谷中书生","墨衣书生 · 沈砚","练功院现在开放六条经脉试学。\n\n先看运笔示范，再亲手练习。六脉考核每题 80 分，首次完成另有签令。",t,new Vector3(-9,0,13),110);
                    var explorer=UnityEngine.Object.FindObjectOfType<ValleyExplorer>();explorer.cameraOffset=new Vector3(0,11,-20);explorer.followCamera.fieldOfView=58;explorer.followCamera.transform.position=explorer.transform.position+explorer.cameraOffset;explorer.followCamera.transform.LookAt(explorer.transform.position+Vector3.up);
                }
                else {
                    for(int i=0;i<4;i++)Spawn("庭院路灯",t,new Vector3(i%2==0?-9:9,0,-3+i/2*11),1.1f,i%2==0?90:-90);
                    for(int i=0;i<6;i++)Spawn("木围栏",t,new Vector3(-12+i*4,0,15),1.3f,0);
                    if(name.StartsWith("Arena")){Camera.main.transform.position=new Vector3(0,6.8f,-15);Camera.main.transform.LookAt(new Vector3(0,2.2f,1));Camera.main.fieldOfView=50;}
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
        }
        public static void RefineComposition()
        {
            Guard();foreach(var name in new[]{"MainMenu","Valley","Arena_Stone","Arena_Bamboo","GestureLab"}){
                var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");var root=GameObject.Find("晴空归云_场景细化").transform;
                var center=name=="Valley"?new Vector3(15,0,25):Vector3.zero;
                foreach(Transform child in root){
                    if(child.name.StartsWith("云岭_")){int index=int.Parse(child.name.Substring(3));var d=child.localPosition-center;d.y=0;child.localPosition=center+d.normalized*(name=="Valley"?190:210)+Vector3.down*18;child.localScale=new Vector3(10+index%3*2,3+index%3,7);}
                    if(child.name=="木围栏")child.gameObject.SetActive(false);
                }
                if(name=="MainMenu"&&root.Find("中景桃林")==null){var grove=new GameObject("中景桃林").transform;grove.SetParent(root,false);for(int i=0;i<7;i++)Spawn("桃树",grove,new Vector3(-18+i*7,-1.5f,40+i%2*10),1.5f,i*49);}
                if(name=="Valley"){var e=UnityEngine.Object.FindObjectOfType<ValleyExplorer>();e.cameraOffset=new Vector3(0,9,-18);PrefabUtility.RecordPrefabInstancePropertyModifications(e);e.followCamera.fieldOfView=58;}
                if(name=="GestureLab"){
                    var lab=UnityEngine.Object.FindObjectOfType<Yibi.UI.GestureLabPresenter>();if(PrefabUtility.IsPartOfPrefabInstance(lab.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(lab.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);var skin=AssetDatabase.LoadAssetAtPath<Sprite>(Selected+"UI/青玉按钮.asset");foreach(var img in lab.routeHighlights){img.sprite=skin;img.type=UnityEngine.UI.Image.Type.Sliced;}
                    lab.detailsText.rectTransform.anchoredPosition=new Vector2(0,-161);lab.detailsText.rectTransform.sizeDelta=new Vector2(318,216);
                    PrefabUtility.SaveAsPrefabAsset(lab.gameObject,"Assets/_Game/Prefabs/GestureLabUI.prefab");
                }
                foreach(var component in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)))if(component!=null&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
        }
    }
}
