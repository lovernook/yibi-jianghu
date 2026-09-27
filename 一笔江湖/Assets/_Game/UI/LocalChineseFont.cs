using UnityEngine;
using UnityEngine.UI;

namespace Yibi.UI
{
    [ExecuteAlways]
    public sealed class LocalChineseFont : MonoBehaviour
    {
        // Uses Windows' installed font; no third-party font file is redistributed.
        private static Font font;
        private void OnEnable()
        {
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            foreach(var text in GetComponentsInChildren<Text>(true))text.font=font;
        }
    }
}
