using UnityEngine;

namespace Yibi.UI
{
    /// <summary>Persists font family preferences, never a generated atlas or a system font file.</summary>
    [CreateAssetMenu(menuName = "一笔江湖/界面/系统字体配置", fileName = "SystemFontProfile")]
    public sealed class SystemFontProfile : ScriptableObject
    {
        public string[] families = {"Microsoft YaHei", "SimHei", "Arial"};
        [Range(8, 128)] public int defaultSize = 24;
    }
}
