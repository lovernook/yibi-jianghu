using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Yibi.World;
using Yibi.Battle;

namespace Yibi.Editor
{
 public static class ImportedModelSetup
 {
  const string Folder="Assets/_Game/Art/Selected/Characters";
  const string Textures="Assets/_Game/Art/剑侠情缘手游模型集合/Documents/Tencent/QQPhoneManager/Application/剑侠情缘/assets/bin/obj/";
  public static void Prepare()
  {
   AssetDatabase.Refresh();
   var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/CharacterCollection.fbx"));
   Extract(source,"234","行者_青衫","m1_003.png",2.6f);
   Extract(source,"232","守卫_玄衣","m1_005.png",2.6f);
   Extract(source,"218","侠客_劲装","m3_001.png",2.6f);
   Extract(source,"260","侠女_素衣","f2_001.png",2.6f);
   Extract(source,"019","村中长者","npc_pt005.png",2.6f);
   Extract(source,"272","洛阳牌坊","cj_luoyang_paifang01.png",9f);
   Extract(source,"273","民居甲","cj_luoyang_jianzhu03.png",8f);
   Extract(source,"274","民居乙","cj_luoyang_jianzhu02.png",8f);
   Extract(source,"275","民居丙","cj_luoyang_jianzhu01.png",9f);
   Extract(source,"271","修炼石台","cj_xinshou_taizi01.png",1.2f);
   UnityEngine.Object.DestroyImmediate(source);AssetDatabase.SaveAssets();
  }
  static void Extract(GameObject source,string suffix,string name,string texture,float height)
  {
   string path=Folder+"/"+name;
   if(AssetDatabase.LoadAssetAtPath<GameObject>(path+".prefab")!=null)return;
   var f=source.GetComponentsInChildren<MeshFilter>().First(x=>x.name=="element3ds.com-default"+suffix);
   var mesh=new Mesh{name=name};mesh.CombineMeshes(new[]{new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}},true,true);
   var b=mesh.bounds;float scale=height/b.size.y;var v=mesh.vertices;
   for(int i=0;i<v.Length;i++)v[i]=(v[i]-new Vector3(b.center.x,b.min.y,b.center.z))*scale;
   mesh.vertices=v;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path+".asset");
   var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+texture);mat.SetFloat("_Smoothness",0);mat.SetFloat("_Cull",0);AssetDatabase.CreateAsset(mat,path+".mat");
   var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=mat;
   PrefabUtility.SaveAsPrefabAsset(root,path+".prefab");UnityEngine.Object.DestroyImmediate(root);
  }
  public static void Preview()
  {
   var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
   int n=0;foreach(var name in new[]{"行者_青衫","守卫_玄衣","侠客_劲装","侠女_素衣","村中长者"}){var g=Spawn(name,null,new Vector3((n++-2)*3,0,0));g.transform.rotation=Quaternion.Euler(0,180,0);}
   Camera.main.transform.position=new Vector3(0,3,-13);Camera.main.transform.LookAt(new Vector3(0,1.3f,0));RenderSettings.ambientLight=Color.gray;
   EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/CharacterPreview.unity");
  }
  static GameObject Spawn(string name,Transform parent,Vector3 position)
  {var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab"));g.transform.SetParent(parent,false);g.transform.localPosition=position;return g;}
  static void Prop(string model,string meshName,string name,float height,bool cutout=false)
  {
   string path=Folder+"/"+name;if(File.Exists(path+".prefab"))return;
   var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/场景模型/Assets/FBX/"+model+".FBX"));
   var f=source.GetComponentsInChildren<MeshFilter>().First(x=>x.name==meshName);var original=f.sharedMesh;
   var mesh=new Mesh{name=name};mesh.CombineMeshes(Enumerable.Range(0,original.subMeshCount).Select(i=>new CombineInstance{mesh=original,subMeshIndex=i,transform=f.transform.localToWorldMatrix}).ToArray(),false,true);
   var b=mesh.bounds;float scale=height/Mathf.Max(b.size.y,.0001f);var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=(v[i]-new Vector3(b.center.x,b.min.y,b.center.z))*scale;mesh.vertices=v;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path+".asset");
   var materials=f.GetComponent<Renderer>().sharedMaterials.Select((m,i)=>{var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name+"_"+i,mainTexture=m.mainTexture};mat.SetFloat("_Smoothness",0);mat.SetFloat("_Cull",0);if(cutout){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}AssetDatabase.CreateAsset(mat,path+"_"+i+".mat");return mat;}).ToArray();
   var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<Renderer>().sharedMaterials=materials;PrefabUtility.SaveAsPrefabAsset(root,path+".prefab");UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(source);
  }
  static void ReplaceVisual(Transform actor,string model,float yaw)
  {
   foreach(var r in actor.GetComponentsInChildren<Renderer>(true))if(r.GetComponent<SequenceEffect>()==null&&r.GetComponent<TextMesh>()==null){r.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
   var old=actor.Find("资源角色");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
   var visual=Spawn(model,actor,Vector3.zero);visual.name="资源角色";visual.transform.localRotation=Quaternion.Euler(0,yaw,0);
  }
  [MenuItem("一笔江湖/素材/完成场景资源替换")]
  public static void Apply()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first");
   Prop("11-14","zhuzishu_01","青竹",8,true);Prop("07-10","Item_greenfields_1001_rock01","山石",10);Prop("49-50","Plant_huangcheng_001","桃树",7,true);
   var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");
   var oldRoot=GameObject.Find("资源布景");if(oldRoot!=null)UnityEngine.Object.DestroyImmediate(oldRoot);
   var root=new GameObject("资源布景");
   var explorer=UnityEngine.Object.FindObjectOfType<ValleyExplorer>();if(PrefabUtility.IsPartOfPrefabInstance(explorer.gameObject))PrefabUtility.UnpackPrefabInstance(explorer.gameObject,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);ReplaceVisual(explorer.visual,"侠客_劲装",0);
   // Keep invisible collision/layout guides editable, replacing their rendered appearance.
   var layout=GameObject.Find("归云谷_可编辑布局");
   foreach(Transform t in layout.transform){
    if(t.name.StartsWith("竹_")||t.name.StartsWith("远山_")){
     foreach(var r in t.GetComponentsInChildren<Renderer>())r.enabled=false;
     var g=Spawn(t.name.StartsWith("竹_")?"青竹":"山石",root.transform,new Vector3(t.position.x,0,t.position.z));if(t.name.StartsWith("远山_"))g.transform.localScale=new Vector3(3,2.5f,3);g.transform.Rotate(0,t.GetSiblingIndex()*37,0);
    }
   }
   var far=GameObject.Find("村落远景_选用资源");if(far!=null)far.SetActive(false);
   for(int i=0;i<6;i++){var g=Spawn(new[]{"民居甲","民居乙","民居丙"}[i%3],root.transform,new Vector3(i%2==0?-17:17,0,-22+(i/2)*19));g.transform.rotation=Quaternion.Euler(0,i%2==0?90:-90,0);var box=g.AddComponent<BoxCollider>();var bounds=g.GetComponent<MeshFilter>().sharedMesh.bounds;box.center=bounds.center;box.size=bounds.size;}
   Spawn("洛阳牌坊",root.transform,new Vector3(0,0,-26));
   for(int i=0;i<8;i++)Spawn("桃树",root.transform,new Vector3(i%2==0?-10:10,0,-28+(i/2)*20));
   var presenter=UnityEngine.Object.FindObjectOfType<ValleyPresenter>();
   for(int i=0;i<presenter.interactionPoints.Length;i++){
    var point=presenter.interactionPoints[i];point.GetComponent<Renderer>().enabled=false;
    var g=Spawn(i==0||i==3?"修炼石台":i==4?"侠女_素衣":"村中长者",root.transform,new Vector3(point.position.x,0,point.position.z));g.transform.rotation=Quaternion.Euler(0,180,0);
   }
   foreach(var cp in presenter.checkpoints){cp.GetComponent<Renderer>().enabled=false;var old=cp.Find("资源试炼标记");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);var g=Spawn("修炼石台",cp,Vector3.zero);g.name="资源试炼标记";g.transform.localScale=new Vector3(.25f,1,.25f);}
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   foreach(var name in new[]{"Arena_Stone","Arena_Bamboo"}){
    scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");var p=UnityEngine.Object.FindObjectOfType<BattlePresenter>();ReplaceVisual(p.leftActor,"侠客_劲装",125);ReplaceVisual(p.rightActor,name=="Arena_Stone"?"村中长者":"行者_青衫",235);
    oldRoot=GameObject.Find("资源擂台");if(oldRoot!=null)UnityEngine.Object.DestroyImmediate(oldRoot);root=new GameObject("资源擂台");
    foreach(var r in UnityEngine.Object.FindObjectsOfType<Renderer>())if(r.name.StartsWith("立柱_")||r.name=="门楣"||r.name=="圆形石台"||r.name.StartsWith("竹竿")||r.name.StartsWith("竹叶"))r.enabled=false;
    var platform=Spawn("修炼石台",root.transform,new Vector3(0,-.9f,0));var size=platform.GetComponent<MeshFilter>().sharedMesh.bounds.size;platform.transform.localScale=new Vector3(16/size.x,1,12/size.z);
    Spawn("洛阳牌坊",root.transform,new Vector3(0,-.2f,10));
    for(int i=0;i<10;i++){var g=Spawn(name=="Arena_Stone"?"桃树":"青竹",root.transform,new Vector3(i<5?-12:12,0,-5+(i%5)*5));g.transform.Rotate(0,i*53,0);}
    for(int i=0;i<3;i++)Spawn("山石",root.transform,new Vector3(-17+i*17,-1,19));
    Camera.main.transform.position=new Vector3(0,9,-15);Camera.main.transform.LookAt(new Vector3(0,1.2f,1));
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   string playerPath="Assets/_Game/Prefabs/PlayerExplorer.prefab";var prefab=PrefabUtility.LoadPrefabContents(playerPath);ReplaceVisual(prefab.GetComponent<ValleyExplorer>().visual,"侠客_劲装",0);PrefabUtility.SaveAsPrefabAsset(prefab,playerPath);PrefabUtility.UnloadPrefabContents(prefab);
   var actor=PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/BattleActor.prefab");ReplaceVisual(actor.transform,"侠客_劲装",125);PrefabUtility.SaveAsPrefabAsset(actor,"Assets/_Game/Prefabs/BattleActor.prefab");PrefabUtility.UnloadPrefabContents(actor);
   AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");
  }
 }
}
