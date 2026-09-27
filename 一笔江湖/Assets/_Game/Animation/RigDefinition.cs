using System;
using UnityEngine;
namespace Yibi.Presentation
{
    [Serializable] public sealed class RigJoint
    {
        public string name;
        public int parent;
        public Vector3 position;
    }
    [CreateAssetMenu(menuName="一笔江湖/角色骨骼配置")]
    public sealed class RigDefinition : ScriptableObject
    {
        public Mesh sourceMesh;
        public Material material;
        public RigJoint[] joints;
        [Tooltip("适合当前 A 姿势角色的自动权重；长袍仍需通过动作预览观察。")]
        public float armThreshold=.31f;
        public float torsoCenterZ=.07f;
        [Range(.1f,1)] public float actionScale=1;
    }
}
