using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using Yibi.UI;
using Yibi.Battle;
using Yibi.World;

namespace Yibi.Editor
{
 public static class SceneFinishing
 {
  static Material Ground(string name,string texture,Vector2 repeat)
  {
   string path="Assets/_Game/Art/Selected/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
   m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/场景模型/Assets/FBX/maps/"+texture);m.mainTextureScale=repeat;m.color=Color.white;m.SetFloat("_Smoothness",0);EditorUtility.SetDirty(m);return m;
  }
  public static void Apply()
  {
   var grass=Ground("资源_苔草地","2x_grnd_terrain_001.psd",Vector2.one*35);
   var road=Ground("资源_石径","ground.psd",new Vector2(2,28));
   var side=Ground("资源_支路","ground.psd",new Vector2(12,2));
   var arena=Ground("资源_擂台地砖","ground.psd",Vector2.one*9);
   foreach(var name in new[]{"Valley","Arena_Stone","Arena_Bamboo","GestureLab","Lobby"}){
    var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");
    foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>()){
     if(r.name=="山谷地面")r.sharedMaterial=grass;
     if(r.name=="南北石径")r.sharedMaterial=road;
     if(r.name=="竹林支路")r.sharedMaterial=side;
     if(r.name=="地面")r.sharedMaterial=arena;
     if(r.name=="修炼石台"&&r.GetComponent<MeshCollider>()==null)r.gameObject.AddComponent<MeshCollider>();
    }
    foreach(var board in UnityEngine.Object.FindObjectsOfType<GestureBoard>(true)){
     // Scoring nodes remain circular; do not put rectangular decorative panels on hit targets.
     foreach(var img in board.GetComponentsInChildren<Image>(true)){
      if(img.transform==board.transform){img.sprite=null;img.color=new Color(.035f,.08f,.12f,.97f);}
      else if(img.name.Contains("穴")||img.name.Contains("Node")||img.name.Contains("节点")){img.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");img.type=Image.Type.Simple;}
      PrefabUtility.RecordPrefabInstancePropertyModifications(img);
     }
    }
    if(name=="Valley"){
     var explorer=UnityEngine.Object.FindObjectOfType<ValleyExplorer>();
     // A scene override predating the prefab replacement can contain both versions. Keep one visual.
     var children=new System.Collections.Generic.List<Transform>();foreach(Transform t in explorer.visual)if(t.name=="资源角色")children.Add(t);
     for(int i=1;i<children.Count;i++)UnityEngine.Object.DestroyImmediate(children[i].gameObject);
     explorer.cameraOffset=new Vector3(0,17,-17);Camera.main.transform.position=explorer.transform.position+explorer.cameraOffset;Camera.main.transform.LookAt(explorer.transform.position);
     PrefabUtility.RecordPrefabInstancePropertyModifications(explorer);
    }
    if(name.StartsWith("Arena"))foreach(var t in UnityEngine.Object.FindObjectsOfType<Text>(true))if(t.name=="标题"){t.text="一 笔 江 湖\n"+(name=="Arena_Stone"?"青石擂台":"听风擂台");PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   }
   AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");
  }
 }
}
