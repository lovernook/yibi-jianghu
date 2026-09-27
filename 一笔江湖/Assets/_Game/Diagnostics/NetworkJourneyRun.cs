using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yibi.Battle;
using Yibi.Networking;
using Yibi.Rules;
using Yibi.World;

namespace Yibi.Diagnostics
{
    [UnityEngine.Scripting.Preserve]
    public sealed class NetworkJourneyRun:MonoBehaviour
    {
        bool host,abortMode,finished,recovered;string directory,role,peer;double expires;IDisposable profile;
        [UnityEngine.Scripting.Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--net-role");if(i<0||i+1>=args.Length)return;
            var g=new GameObject("NetworkLifecycleEvidence");DontDestroyOnLoad(g);var run=g.AddComponent<NetworkJourneyRun>();run.host=args[i+1]=="host";run.role=run.host?"host":"join";run.peer=run.host?"join":"host";
            int d=Array.IndexOf(args,"--net-dir");run.directory=args[d+1];run.abortMode=Array.IndexOf(args,"--net-abort")>=0;run.expires=Time.realtimeSinceStartupAsDouble+480;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;Directory.CreateDirectory(directory);profile=ProfileStore.UseTransientProfile(new PlayerProfile());
            var steps=Run();while(!finished){object current=null;try{if(!steps.MoveNext())break;current=steps.Current;}catch(Exception ex){Fail(ex.ToString());yield break;}yield return current;}
        }
        void Update(){if(!finished&&expires>0&&Time.realtimeSinceStartupAsDouble>expires)Fail("Timed out: "+(NetSession.Instance==null?"no session":NetSession.Instance.Status));}
        IEnumerator Run()
        {
            yield return SceneManager.LoadSceneAsync("Lobby");yield return null;yield return null;
            var lobby=FindObjectOfType<LobbyPresenter>();var net=NetSession.Ensure();lobby.address.text="127.0.0.1";
            string roomFile=Path.Combine(directory,"room.txt");
            if(host)lobby.Create();else{while(!File.Exists(roomFile))yield return null;lobby.roomCode.text=File.ReadAllText(roomFile);lobby.Join();}
            while(net.Seat<0||!net.Synchronized)yield return null;
            if(host)Write("room.txt",net.Room);
            Require(lobby.createButton!=null&&lobby.roomSummary!=null,"saved lobby references");lobby.Ready();
            string lastMatch=null;int submissions=0;
            for(int round=0;round<(abortMode?1:5);round++){
                while(net.State==null||net.State.matchId==lastMatch||FindObjectOfType<BattlePresenter>()==null)yield return null;
                yield return null;var battle=FindObjectOfType<BattlePresenter>();var controls=battle.GetComponent<NetworkBattlePanel>();Require(controls!=null&&controls.room!=null,"saved battle controls");
                lastMatch=net.State.matchId;net.LatencyMilliseconds=round==1?100:round==2?300:0;bool injected=false,observedRecovery=false;
                Debug.Log("YIBI_NETWORK_ROUND "+round+" match="+lastMatch+" delay_per_direction="+net.LatencyMilliseconds);
                if(abortMode){
                    Write(role+"-active.txt",lastMatch);while(!net.Aborted){if(net.Recovering){Require(!net.CanAct,"recovery locks actions");observedRecovery=true;}yield return null;}
                    yield return null;Require(net.State==null&&battle.State.winner==-2&&battle.resultPanel.activeSelf,"server loss cannot create local winner");
                    Require(battle.resultLabel.text.Contains("不记胜负"),"aborted result shown");battle.Restart();while(SceneManager.GetActiveScene().name!="Lobby")yield return null;
                    Write(role+"-abort.txt","PASS observedRecovery="+observedRecovery);break;
                }
                while(net.State==null||net.State.winner==-2){
                    Require(!net.Aborted,"unexpected abort "+net.Status);
                    if(net.Recovering){observedRecovery=true;Require(!net.CanAct&&!net.Synchronized,"no input before resynchronization");}
                    if(host&&round==1&&observedRecovery&&!net.Recovering&&net.Synchronized){Require(net.LastCommandStatus=="Accepted","query accepted command instead of resending");recovered=true;observedRecovery=false;}
                    if(net.State!=null&&battle.State==net.State&&net.CanAct&&net.State.activeSeat==net.Seat&&battle.basicButton.interactable){
                        if(host&&round==1&&!injected){net.DropNextActionResponseForTest=true;injected=true;}
                        string id="basic";foreach(var candidate in new[]{"lieshi","liuhuo","dianxue","huifeng","qingxin","jiefan"})if(Array.IndexOf(net.State.fighters[net.Seat].loadout,candidate)>=0&&BattleReducer.CanUse(net.State,net.Seat,candidate)==null){id=candidate;break;}
                        if(id=="basic")battle.Basic();else{
                            battle.SelectSkill(Array.IndexOf(net.State.fighters[net.Seat].loadout,id));var nodes=battle.board.template.nodes;
                            battle.board.Begin(new Point2(nodes[0].x,nodes[0].y),0);for(int j=1;j<nodes.Length-1;j++)battle.board.Move(new Point2(nodes[j].x,nodes[j].y),j*.1);
                            var end=nodes[nodes.Length-1];battle.board.Finish(new Point2(end.x,end.y),1);Require(battle.board.LastResult.Valid,"valid drawing reaches confirm");battle.Confirm();
                        }submissions++;yield return new WaitForSecondsRealtime(.35f);
                    }yield return null;
                }
                if(host&&round==1)Require(recovered,"lost reply recovery completed");
                string json=JsonUtility.ToJson(net.State,true);Write(role+"-round-"+round+".json",json);
                while(!File.Exists(Path.Combine(directory,peer+"-round-"+round+".json")))yield return null;
                Require(File.ReadAllText(Path.Combine(directory,peer+"-round-"+round+".json"))==json,"identical final authoritative snapshots");
                Debug.Log("YIBI_NETWORK_MATCH_PASS round="+round+" revision="+net.State.revision+" winner="+net.State.winner);
                if(round==0||round==4){yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Path.Combine(directory,role+"-round-"+round+".png"));yield return new WaitForSecondsRealtime(.3f);}
                if(round==4)break;
                if(round==2){
                    // One peer requests the room reset; both clients must navigate there.
                    if(host)controls.Room();while(SceneManager.GetActiveScene().name!="Lobby")yield return null;yield return null;yield return null;
                    Require(net.State==null&&net.Seat>=0&&net.Synchronized,"room identity retained after reset");lobby=FindObjectOfType<LobbyPresenter>();lobby.Cycle(0);lobby.Mindset();lobby.Ready();
                }else controls.Rematch();
            }
            net.Leave();while(net.Leaving||NetSession.WorkerCount>0)yield return null;
            Require(NetSession.WorkerCount==0&&!net.Connected,"transport workers released");Write(role+"-pass.txt","PASS rounds="+(abortMode?1:5)+" submissions="+submissions+" recovered="+recovered+" workers="+NetSession.WorkerCount);
            Debug.Log("YIBI_NETWORK_JOURNEY_PASS "+role);finished=true;profile.Dispose();profile=null;Application.Quit(0);
        }
        void Write(string name,string contents){string path=Path.Combine(directory,name);File.WriteAllText(path+".tmp",contents);File.Move(path+".tmp",path);}
        static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
        void Fail(string reason){if(finished)return;finished=true;Debug.LogError("YIBI_NETWORK_JOURNEY_FAIL "+reason);File.WriteAllText(Path.Combine(directory,role+"-fail.txt"),reason);profile?.Dispose();profile=null;NetSession.Instance?.Disconnect();Application.Quit(1);}
        void OnDestroy(){profile?.Dispose();}
    }
}
