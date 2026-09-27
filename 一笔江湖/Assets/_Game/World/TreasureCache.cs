using UnityEngine;
using Yibi.Battle;
namespace Yibi.World
{
    public sealed class TreasureCache:MonoBehaviour
    {
        public string stableId;
        public int tickets=1;
        public string progressFact="cache.count";
        public GameObject glow;
        string cachedId,cachedKey;
        string Key{get{if(cachedId!=stableId||cachedKey==null){cachedId=stableId;cachedKey="cache-"+stableId;}return cachedKey;}}
        public bool Claimed=>ProfileStore.Current!=null&&ProfileStore.Current.achievements.Contains(Key);
        private void Start(){ProfileStore.Load();Refresh();}
        public bool Collect(){if(string.IsNullOrWhiteSpace(stableId))return false;bool reward=ProfileStore.Reward(Key,tickets,progressFact);Refresh();return reward;}
        private void Refresh(){if(glow!=null)glow.SetActive(!Claimed);}
    }
}
