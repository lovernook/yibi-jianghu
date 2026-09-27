using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yibi.Rules;
using Yibi.Networking;
namespace Yibi.World
{
    public sealed class LobbyPresenter:MonoBehaviour
    {
        public InputField address,roomCode;
        public Text status,readiness,mindsetText;
        public Button[] loadoutButtons;
        public Button createButton,joinButton,readyButton,resumeButton;
        public Text roomSummary;
        private float nextRefresh;
        private string[] loadout={"dianxue","lieshi","huifeng"};
        private string mindset="shouzhuo";
        private NetSession net;
        private void Start(){net=NetSession.Ensure();loadout=(string[])net.LocalLoadout.Clone();mindset=net.LocalMindset;RefreshLoadout();}
        private void Update(){if(net.Synchronized&&net.State!=null&&net.State.winner==-2&&net.Seat>=0){SceneManager.LoadScene("Arena_Stone");return;}if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.2f;status.text=net.Status;readiness.text="席位一 "+(net.Readiness[0]?"已准备":"未准备")+"    席位二 "+(net.Readiness[1]?"已准备":"未准备");
            if(createButton!=null){bool can=!net.Connecting&&!net.Leaving&&net.Seat<0&&!net.Recovering;createButton.interactable=joinButton.interactable=can;readyButton.interactable=net.Connected&&net.Synchronized&&net.Seat>=0;resumeButton.interactable=net.Recovering&&!net.Connecting;readyButton.GetComponentInChildren<Text>().text=net.Seat>=0&&net.Readiness[net.Seat]?"取消准备":"确认构筑 · 准备";roomSummary.text=net.Recovering?"自动恢复中 · 剩余 "+Mathf.CeilToInt((float)net.RecoveryRemaining)+" 秒":net.Seat<0?"创建房间后，将六位房号告诉另一位玩家。":"房号 "+net.Room+" · 你是席位 "+(net.Seat+1)+" · "+net.Players+" / 2 人";}}
        public void Create(){if(net.Connected)net.Send("CreateRoom");else net.Connect(address.text.Trim(),"CreateRoom");}
        public void Join(){if(net.Connected)net.Send("JoinRoom",p=>p.room=roomCode.text.Trim());else net.Connect(address.text.Trim(),"JoinRoom",roomCode.text.Trim());}
        public void Ready(){if(net.Seat<0)return;if(net.Readiness[net.Seat]){net.Send("Ready",p=>p.ready=false);return;}net.Send("SelectLoadout",p=>{p.loadout=loadout;p.mindset=mindset;});net.Send("Ready",p=>p.ready=true);}
        public void Resume(){net.Resume();}
        public void Leave(){net.Leave();SceneManager.LoadScene("Valley");}
        public void Cycle(int slot){int start=Array.IndexOf(BattleContent.Ids,loadout[slot]);for(int i=1;i<=6;i++){string id=BattleContent.Ids[(start+i)%6];if(Array.IndexOf(loadout,id)<0){loadout[slot]=id;break;}}RefreshLoadout();}
        public void Mindset(){mindset=mindset=="shouzhuo"?"fanzhao":"shouzhuo";RefreshLoadout();}
        private void RefreshLoadout(){for(int i=0;i<3;i++)loadoutButtons[i].GetComponentInChildren<Text>().text=BattleContent.Name(loadout[i]);mindsetText.text="心法 · "+(mindset=="shouzhuo"?"守拙":"反照");if(net!=null&&net.Connected&&net.Seat>=0)net.Send("SelectLoadout",p=>{p.loadout=loadout;p.mindset=mindset;});}
    }
}
