using UnityEditor;
using Yibi.UI;

namespace Yibi.Editor
{
    [InitializeOnLoad]
    public static class SystemFontCacheLifecycle
    {
        static SystemFontCacheLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= SavedChineseFont.ReleaseSharedFonts;
            AssemblyReloadEvents.beforeAssemblyReload += SavedChineseFont.ReleaseSharedFonts;
            EditorApplication.quitting -= SavedChineseFont.ReleaseSharedFonts;
            EditorApplication.quitting += SavedChineseFont.ReleaseSharedFonts;
        }
    }
}
