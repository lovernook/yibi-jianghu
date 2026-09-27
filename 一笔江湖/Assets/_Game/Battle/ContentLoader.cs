using UnityEngine;
using Yibi.Rules;
namespace Yibi.Battle
{
    public static class ContentLoader
    {
        public static string Hash{get;private set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load(){Application.targetFrameRate=60;var text=Resources.Load<TextAsset>("ContentCatalog");var catalog=text==null?ContentCatalog.Default():JsonUtility.FromJson<ContentCatalog>(text.text);catalog.Install();Hash=catalog.Hash();}
    }
}
