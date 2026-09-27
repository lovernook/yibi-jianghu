using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yibi.Rules;
using Yibi.Networking;

namespace Yibi.Battle
{
    public sealed class NetworkSmoke:MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot(){var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"--pvp-host")>=0||Array.IndexOf(args,"--pvp-join")>=0){var g=new GameObject("AutomatedNetworkTest");DontDestroyOnLoad(g);g.AddComponent<NetworkSmoke>();}}
        private IEnumerator Start()
        {
            Application.runInBackground=true;var args=Environment.GetCommandLineArgs();bool host=Array.IndexOf(args,"--pvp-host")>=0;int a=Array.IndexOf(args,"--session-dir");if(a<0||a+1>=args.Length){Application.Quit(2);yield break;}
            string dir=args[a+1];Directory.CreateDirectory(dir);string roomPath=Path.Combine(dir,"room.txt");var net=NetSession.Ensure();float until=Time.realtimeSinceStartup+120;
            if(host)net.Connect("127.0.0.1","CreateRoom");else{while(!File.Exists(roomPath)&&Time.realtimeSinceStartup<until)yield return null;if(!File.Exists(roomPath)){Application.Quit(3);yield break;}net.Connect("127.0.0.1","JoinRoom",File.ReadAllText(roomPath).Trim());}
            while(net.Seat<0&&Time.realtimeSinceStartup<until)yield return null;
            if(net.Seat<0){Debug.LogError("PVP_SMOKE could not join: "+net.Status);Application.Quit(4);yield break;}
            if(host){File.WriteAllText(roomPath+".tmp",net.Room);File.Move(roomPath+".tmp",roomPath);}
            net.Send("SelectLoadout",p=>{p.loadout=new[]{"dianxue","lieshi","huifeng"};p.mindset="shouzhuo";});net.Send("Ready",p=>p.ready=true);
            while(net.State==null&&Time.realtimeSinceStartup<until)yield return null;
            if(net.State==null){Debug.LogError("PVP_SMOKE match did not start");Application.Quit(5);yield break;}
            yield return SceneManager.LoadSceneAsync("Arena_Stone");yield return null;var presenter=FindObjectOfType<BattlePresenter>();int submitted=0;
            while(net.State!=null&&net.State.winner==-2&&Time.realtimeSinceStartup<until){
                if(presenter.State==net.State&&net.State.activeSeat==net.Seat&&!net.Pending&&presenter.basicButton.interactable){
                    string id=BattleReducer.CanUse(net.State,net.Seat,"lieshi")==null?"lieshi":BattleReducer.CanUse(net.State,net.Seat,"dianxue")==null?"dianxue":"basic";
                    if(id=="basic")presenter.Basic();else{
                        presenter.SelectSkill(Array.IndexOf(net.State.fighters[net.Seat].loadout,id));var nodes=presenter.board.template.nodes;
                        presenter.board.Begin(new Point2(nodes[0].x,nodes[0].y),0);for(int i=1;i<nodes.Length-1;i++)presenter.board.Move(new Point2(nodes[i].x,nodes[i].y),i*.1);
                        var end=nodes[nodes.Length-1];presenter.board.Finish(new Point2(end.x,end.y),1);presenter.Confirm();
                    }submitted++;yield return new WaitForSecondsRealtime(.35f);
                }yield return null;
            }
            bool passed=net.State!=null&&net.State.winner!=-2;var state=net.State;
            if(passed){File.WriteAllText(Path.Combine(dir,host?"host-state.json":"join-state.json"),JsonUtility.ToJson(state,true));Debug.Log("YIBI_PVP_SMOKE PASS seat="+net.Seat+" match="+state.matchId+" revision="+state.revision+" winner="+state.winner+" submitted="+submitted);}
            else Debug.LogError("YIBI_PVP_SMOKE FAILED "+net.Status);
            yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,host?"host.png":"join.png"));yield return new WaitForSecondsRealtime(.5f);net.Leave();yield return new WaitForSecondsRealtime(.5f);Application.Quit(passed?0:1);
        }
    }
}
