using UnityEngine;
using UnityEngine.UI;
namespace Yibi.UI
{
    // Inactive nested Canvases can lose their effective sorting state until enabled.
    // Keep the intended order serialized and reapply when the saved panel opens.
    [RequireComponent(typeof(Canvas),typeof(GraphicRaycaster))]
    public sealed class ModalLayer:MonoBehaviour
    {
        public int order=20;
        private void OnEnable(){var canvas=GetComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=order;transform.SetAsLastSibling();}
    }
}
