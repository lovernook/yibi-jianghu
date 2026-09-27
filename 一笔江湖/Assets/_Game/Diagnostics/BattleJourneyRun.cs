using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yibi.App;
using Yibi.Battle;
using Yibi.Rules;
using Yibi.Presentation;

namespace Yibi.Diagnostics
{
    // Opt-in native player checks; installs an isolated profile before normal scene startup.
    [UnityEngine.Scripting.Preserve]
    public sealed class BattleJourneyRun : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string utc,unity,failure;
            public bool passed,finished;
            public int errorLogCount,acceptedPlayerActions,lastRevision,winner;
            public double elapsedSeconds;
            public string[] steps,errors;
            public string note="Saved UI, scripted Begin/Move/Finish pipeline and actual offline reducer. Not a human mouse, visual or cross-computer certification.";
        }
        readonly Report report=new Report();
        readonly List<string> steps=new List<string>(),errors=new List<string>();
        string output;double started;IDisposable profile;bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--battle-smoke-report");if(at<0)return;
            if(at+1>=args.Length){Application.Quit(2);return;}
            var host=new GameObject("BattleJourneyEvidence");DontDestroyOnLoad(host);var run=host.AddComponent<BattleJourneyRun>();
            run.output=Path.GetFullPath(args[at+1]);run.started=Time.realtimeSinceStartupAsDouble;
            run.profile=ProfileStore.UseTransientProfile(new PlayerProfile());ProfileStore.NpcStrategy=0;
            Application.runInBackground=true;Application.logMessageReceived+=run.Capture;
        }
        IEnumerator Start()
        {
            report.utc=DateTime.UtcNow.ToString("O");report.unity=Application.unityVersion;
            var routines=new Stack<IEnumerator>();routines.Push(Run());
            while(routines.Count>0&&!finished){object current=null;bool moved;
                try{moved=routines.Peek().MoveNext();if(moved)current=routines.Peek().Current;}
                catch(Exception ex){Finish(false,ex.ToString());yield break;}
                if(!moved){(routines.Pop() as IDisposable)?.Dispose();continue;}
                var nested=current as IEnumerator;if(nested!=null){routines.Push(nested);continue;}yield return current;
            }
        }
        IEnumerator Run()
        {
            yield return Wait(()=>GameRoot.Instance!=null&&GameRoot.Instance.CurrentProcedure=="ProcedureMainMenu","main menu");
            Require(GameNavigation.GoTo(GameScene.Arena_Stone),"Arena navigation rejected");
            yield return Wait(()=>SceneManager.GetActiveScene().name=="Arena_Stone"&&!GameNavigation.IsInputBlocked,"arena");yield return null;
            var p=FindObjectOfType<BattlePresenter>();Require(p!=null&&p.hud!=null&&p.presentation!=null,"Saved Stage C HUD/profile missing");
            Require(p.leftActor.GetComponentInChildren<CharacterMotion>().profile!=null,"Saved motion profile missing");
            Require(p.feedback.attackEffects.Length==2&&p.feedback.attackEffects[0]!=null,"Saved attack effect missing");Record("saved-hud-presentation-motion-and-fx");
            p.skillButtons[0].onClick.Invoke();var board=p.board;var nodes=board.template.nodes;int revision=p.State.revision;
            board.ShowSample(BattleContent.Perfect(board.template.stableId));p.confirmButton.onClick.Invoke();Require(p.State.revision==revision&&!p.confirmButton.interactable,"Program sample submitted");
            board.Begin(new Point2(.98,.98),0);board.Finish(new Point2(nodes[nodes.Length-1].x,nodes[nodes.Length-1].y),1);
            Require(!board.LastResult.Valid&&p.hud.recoveryLabel.text.Contains("1 号"),"Wrong start explanation missing");
            p.Clear();Require(!p.confirmButton.interactable,"Cleared stroke can confirm");
            Draw(p,true,false);Require(!board.LastResult.Valid&&board.LastResult.ErrorCode==GestureError.OutOfBounds,"Out-of-bounds did not invalidate stroke");
            p.Clear();Draw(p,false,true);Require(!board.LastResult.Valid,"Skipped node was accepted");
            p.Clear();Draw(p,false,false);Require(board.LastResult.Valid&&p.confirmButton.interactable,"Valid redraw did not recover");
            Require(p.hud.gradeLabel.text.Contains("1.20")&&p.hud.breakdownLabel.text.Contains("60"),"Actual multiplier/breakdown missing");
            board.Begin(new Point2(nodes[0].x,nodes[0].y),3);Require(!p.confirmButton.interactable,"New stroke left old confirmation enabled");
            p.Clear();Draw(p,false,false);p.confirmButton.onClick.Invoke();p.confirmButton.onClick.Invoke();
            Require(p.State.revision==revision+1,"Confirmation did not accept exactly one action");report.acceptedPlayerActions++;Record("sample-rejection-three-errors-redraw-single-submit");
            yield return new WaitForSecondsRealtime(.1f);
            Require(p.leftActor.GetComponentInChildren<CharacterMotion>().CurrentAction=="Attack","Saved attack action not playing");
            yield return Wait(()=>p.State.winner!=-2||p.basicButton.interactable,"next player turn");Record("animation-and-npc-turn-return");
            int stepsLimit=0;
            while(p.State.winner==-2&&stepsLimit++<30){
                yield return Wait(()=>p.State.winner!=-2||p.basicButton.interactable,"action availability");if(p.State.winner!=-2)break;
                string id=BattleReducer.CanUse(p.State,0,"lieshi")==null?"lieshi":BattleReducer.CanUse(p.State,0,"dianxue")==null?"dianxue":p.State.fighters[0].energy<2?"rest":"basic";
                if(id=="basic")p.basicButton.onClick.Invoke();else if(id=="rest")p.restButton.onClick.Invoke();else{p.skillButtons[Array.IndexOf(p.State.fighters[0].loadout,id)].onClick.Invoke();Draw(p,false,false);p.confirmButton.onClick.Invoke();}
                report.acceptedPlayerActions++;yield return null;
            }
            yield return Wait(()=>p.resultPanel.activeSelf,"result panel");
            Require(p.State.winner!=-2&&!string.IsNullOrEmpty(p.hud.settlementLabel.text),"Round results missing");report.lastRevision=p.State.revision;report.winner=p.State.winner;Record("actual-offline-match-to-saved-result-ui");
            p.Restart();Require(p.State.revision==0&&!p.resultPanel.activeSelf,"Restart did not reset");Record("restart-clears-presentation");
            Require(errors.Count==0,"Unity error log captured");Finish(true,null);
        }
        static void Draw(BattlePresenter p,bool outOfBounds,bool skip)
        {
            var nodes=p.board.template.nodes;double start=Time.unscaledTimeAsDouble;p.board.Begin(new Point2(nodes[0].x,nodes[0].y),start);
            if(outOfBounds)p.board.Move(new Point2(-.2,.3),start+.05);
            for(int i=1;i<nodes.Length-1;i++)if(!skip||i!=1)p.board.Move(new Point2(nodes[i].x,nodes[i].y),start+i*.1);
            var end=nodes[nodes.Length-1];p.board.Finish(new Point2(end.x,end.y),start+1);
        }
        static IEnumerator Wait(Func<bool> condition,string step){double until=Time.realtimeSinceStartupAsDouble+40;while(!condition()){Require(Time.realtimeSinceStartupAsDouble<until,"Timeout: "+step);yield return null;}}
        void Capture(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        void Update(){if(!finished&&Time.realtimeSinceStartupAsDouble-started>180)Finish(false,"Overall timeout");}
        void Record(string step){steps.Add(step);Write();}
        void Write(){report.finished=finished;report.elapsedSeconds=Time.realtimeSinceStartupAsDouble-started;report.steps=steps.ToArray();report.errors=errors.ToArray();report.errorLogCount=errors.Count;Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));}
        void Finish(bool passed,string failure){if(finished)return;finished=true;report.passed=passed;report.failure=failure;try{Write();}catch(Exception ex){Debug.LogError(ex);passed=false;}Cleanup();Debug.Log(passed?"YIBI_BATTLE_SMOKE_PASS":"YIBI_BATTLE_SMOKE_FAIL "+failure);Application.Quit(passed?0:1);}
        void Cleanup(){Application.logMessageReceived-=Capture;profile?.Dispose();profile=null;}
        void OnDestroy(){Cleanup();}
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
