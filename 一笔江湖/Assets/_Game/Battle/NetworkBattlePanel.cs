using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Yibi.Networking;
namespace Yibi.Battle
{
    public sealed class NetworkBattlePanel:MonoBehaviour
    {
        public BattlePresenter battle;public GameObject panel;public Text status;public Button rematch,room,recover;float next;
        void Update(){var net=NetSession.Instance;bool online=battle.IsOnline;panel.SetActive(online&&!battle.drawingPanel.activeSelf);if(!online||net==null||Time.unscaledTime<next)return;next=Time.unscaledTime+.2f;
            bool ended=net.State!=null&&net.State.winner!=-2;
            recover.interactable=net.Recovering&&!net.Connecting;rematch.interactable=ended&&net.Connected&&net.Players==2;room.interactable=ended&&net.Connected||net.Aborted;
            rematch.GetComponentInChildren<Text>().text=net.Seat>=0&&net.RematchVotes[net.Seat]?"取消再战邀请":"再战一局";
            status.text=net.Recovering?"连接中断 · 恢复剩余 "+Mathf.CeilToInt((float)net.RecoveryRemaining)+" 秒 · 回合继续计时":net.Aborted?net.Status:ended?"再战需双方同意 · "+(net.RematchVotes[0]?"一已同意":"一未同意")+" / "+(net.RematchVotes[1]?"二已同意":"二未同意"):net.Players<2?"对手已离开":net.PeersConnected.Length==2&&(!net.PeersConnected[0]||!net.PeersConnected[1])?"对手暂时断线 · 最多保留 30 秒":net.Status;
        }
        public void Rematch(){NetSession.Instance?.Rematch();}
        public void Room(){var n=NetSession.Instance;if(n==null||n.Aborted){n?.Leave();SceneManager.LoadScene("Lobby");}else n.ReturnToRoom();}
        public void Recover(){NetSession.Instance?.Resume();}
    }
}
