using System.Collections.Generic;
using UnityEngine;
namespace Yibi.Core
{
    public sealed class SceneMemoryPolicy:MonoBehaviour
    {
        [Tooltip("Standalone only: mesh vertices are not read by gameplay scripts; source meshes remain editable in Unity.")]
        public bool releaseMeshCpuCopies=true;
        void Start()
        {
            if(Application.isEditor||!releaseMeshCpuCopies)return;
            var meshes=new HashSet<Mesh>();var colliderMeshes=new HashSet<Mesh>();
            foreach(var root in gameObject.scene.GetRootGameObjects()){
                foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh!=null)meshes.Add(f.sharedMesh);
                foreach(var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(s.sharedMesh!=null)meshes.Add(s.sharedMesh);
                foreach(var c in root.GetComponentsInChildren<MeshCollider>(true))if(c.sharedMesh!=null)colliderMeshes.Add(c.sharedMesh);
            }
            // Colliders are already deserialized/cooked before Start. No runtime mesh deformation uses these assets.
            foreach(var mesh in meshes)if(mesh.isReadable&&!colliderMeshes.Contains(mesh))mesh.UploadMeshData(true);
            // The collection is local so it cannot keep old scenes' meshes alive.
        }
    }
}
