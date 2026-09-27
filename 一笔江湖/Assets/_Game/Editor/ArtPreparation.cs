using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace Yibi.Editor
{
    public static class ArtPreparation
    {
        public const string Output="Assets/_Game/Art/Selected";
        [MenuItem("一笔江湖/素材/整理选中场景模型")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            foreach(var id in new[]{"61","66","49-50"})PrepareModel(id);
            AssetDatabase.SaveAssets();
        }
        private static void PrepareModel(string id)
        {
            string path=Output+"/Environment_"+id+".prefab";if(File.Exists(path))return;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/场景模型/Assets/FBX/"+id+".FBX");
            var instance=UnityEngine.Object.Instantiate(source);var groups=new Dictionary<Material,List<CombineInstance>>();
            var bounds=new Bounds();bool first=true;
            foreach(var f in instance.GetComponentsInChildren<MeshFilter>()){
                var r=f.GetComponent<MeshRenderer>();if(r==null||f.sharedMesh==null)continue;
                if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);
                for(int i=0;i<f.sharedMesh.subMeshCount;i++){
                    var mat=r.sharedMaterials[Mathf.Min(i,r.sharedMaterials.Length-1)];if(mat==null)continue;
                    if(!groups.ContainsKey(mat))groups.Add(mat,new List<CombineInstance>());
                    groups[mat].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=f.transform.localToWorldMatrix});
                }
            }
            var root=new GameObject("环境_"+id);float scale=80/Mathf.Max(bounds.size.x,bounds.size.z);int n=0;
            foreach(var pair in groups){
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Environment_"+id+"_"+n};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                var verts=mesh.vertices;for(int i=0;i<verts.Length;i++)verts[i]=(verts[i]-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;mesh.vertices=verts;mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Output+"/"+mesh.name+".asset");
                var go=new GameObject(pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var material=new Material(pair.Key);material.name=id+"_"+pair.Key.name;material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetFloat("_Smoothness",.05f);
                AssetDatabase.CreateAsset(material,Output+"/Material_"+id+"_"+n+".mat");go.GetComponent<MeshRenderer>().sharedMaterial=material;n++;
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("整理场景模型 "+id+"：按材质合并为 "+n+" 个可编辑渲染节点");
        }
        [MenuItem("一笔江湖/素材/打开资源预览场景")]
        public static void Preview()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save current scene first");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Environment_66.prefab");PrefabUtility.InstantiatePrefab(prefab);
            Camera.main.transform.position=new Vector3(45,65,-65);Camera.main.transform.LookAt(new Vector3(0,4,0));Camera.main.farClipPlane=500;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.7f,.75f);
            var light=UnityEngine.Object.FindObjectOfType<Light>();light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(50,-30,0);
            EditorSceneManager.SaveScene(scene,"Assets/_Game/Scenes/ArtPreview.unity");
        }
    }
}
