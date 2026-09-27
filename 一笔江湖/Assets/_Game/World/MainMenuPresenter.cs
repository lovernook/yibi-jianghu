using UnityEngine;
using UnityEngine.UI;
using Yibi.Battle;
using Yibi.App;
namespace Yibi.World
{
    public sealed class MainMenuPresenter:MonoBehaviour
    {
        public Text continueText,progress;
        public GameObject help;
        private void OnEnable(){ProfileStore.Changed+=RefreshProgress;}
        private void OnDisable(){ProfileStore.Changed-=RefreshProgress;}
        private void Start(){ProfileStore.Load();RefreshProgress();help.SetActive(false);}
        private void RefreshProgress()
        {
            var profile=ProfileStore.Current;if(profile==null)return;
            continueText.text=profile.achievements.Count>0||profile.claimedQuests.Count>0||profile.questFacts.Count>0?"继续归云之旅":"踏入归云谷";
            var tracked=ProfileStore.Progress.GetTracked();
            progress.text=tracked!=null?"归云篇 · "+tracked.Title:"归云篇 · 主线已完成";
        }
        public void Continue(){if(GameNavigation.IsInputBlocked)return;ProfileStore.HasReturnPosition=false;GameNavigation.GoTo(GameScene.Valley);}
        public void Practice(){GameNavigation.GoTo(GameScene.GestureLab);}
        public void Multiplayer(){GameNavigation.GoTo(GameScene.Lobby);}
        public void ToggleHelp(){help.SetActive(!help.activeSelf);}
        public void Quit(){ProfileStore.Save();Application.Quit();}
    }
}
