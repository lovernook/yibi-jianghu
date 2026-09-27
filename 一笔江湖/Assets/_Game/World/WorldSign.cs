using UnityEngine;
namespace Yibi.World
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class WorldSign:MonoBehaviour
    {
        private static Font font;
        private void OnEnable(){if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},32);var text=GetComponent<TextMesh>();text.font=font;GetComponent<MeshRenderer>().sharedMaterial=font.material;}
        private void LateUpdate(){if(Camera.main!=null)transform.rotation=Camera.main.transform.rotation;}
    }
}
