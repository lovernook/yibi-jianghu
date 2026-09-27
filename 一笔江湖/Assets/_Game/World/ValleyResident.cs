using UnityEngine;
namespace Yibi.World
{
    public sealed class ValleyResident : MonoBehaviour
    {
        public string displayName;
        [TextArea(3,8)] public string dialogue;
        public float radius=3.5f;
    }
}
