using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Yibi.UI
{
    /// <summary>A saved family profile creates one shared live OS font per configuration.</summary>
    [ExecuteAlways]
    public sealed class SavedChineseFont : MonoBehaviour
    {
        public SystemFontProfile profile;
        private static readonly string[] defaultFamilies = {"Microsoft YaHei", "SimHei", "Arial"};
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        private void OnEnable() { Apply(); }

        [ContextMenu("应用字体配置")]
        public void Apply()
        {
            var font = GetSharedFont(profile);
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
        }

        public static Font GetSharedFont(SystemFontProfile configuration)
        {
            var families = configuration != null && configuration.families != null && configuration.families.Length > 0
                ? configuration.families : defaultFamilies;
            int size = configuration == null ? 24 : Mathf.Clamp(configuration.defaultSize, 8, 128);
            string key = size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + string.Join("\u001f", families);
            Font font;
            // With domain reload disabled Unity can keep the Font wrapper but release its
            // generated atlas during a Play transition. A non-null wrapper alone is insufficient.
            if (fonts.TryGetValue(key, out font) && font != null && font.material != null && font.material.mainTexture != null) return font;
            font = Font.CreateDynamicFontFromOSFont(families, size);
            font.name = "Guiyun Live OS Font";
            font.hideFlags = HideFlags.HideAndDontSave;
            if (font.material != null) {
                font.material.hideFlags = HideFlags.HideAndDontSave;
                if (font.material.mainTexture != null) font.material.mainTexture.hideFlags = HideFlags.HideAndDontSave;
            }
            fonts[key] = font;
            return font;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterShutdown()
        {
            // Keep the cache on Play transitions when domain reload is disabled.
            Application.quitting -= ReleaseSharedFonts;
            Application.quitting += ReleaseSharedFonts;
        }

        /// <summary>Only editor reload/shutdown and player shutdown own this shared lifetime.</summary>
        public static void ReleaseSharedFonts()
        {
            foreach (var font in fonts.Values) {
                if (font == null) continue;
                if (Application.isPlaying) Destroy(font); else DestroyImmediate(font);
            }
            fonts.Clear();
        }
    }
}
