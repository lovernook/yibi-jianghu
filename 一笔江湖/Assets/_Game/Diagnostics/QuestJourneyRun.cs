using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yibi.App;
using Yibi.Battle;
using Yibi.Rules;
using Yibi.World;

namespace Yibi.Diagnostics
{
    // Opt-in release-player scenario; isolated state is installed before scene Awake/Start.
    [UnityEngine.Scripting.Preserve]
    public sealed class QuestJourneyRun : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public string utc, unity, failure;
            public bool finished, passed;
            public int tickets, fragments, completedQuests, errorLogCount;
            public double elapsedSeconds;
            public string[] steps, errors;
            public string note="Native player scripted input pipeline, saved scene UI and isolated JSON reload; not human mouse or travel difficulty certification.";
        }
        private readonly Report report=new Report();
        private readonly List<string> steps=new List<string>(),errors=new List<string>();
        private string output;
        private double started;
        private IDisposable profile,reloaded;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--quest-smoke-report");
            if(index<0)return;
            if(index+1>=args.Length){Application.Quit(2);return;}
            var host=new GameObject("QuestJourneyEvidence");DontDestroyOnLoad(host);
            var runner=host.AddComponent<QuestJourneyRun>();
            runner.output=Path.GetFullPath(args[index+1]);runner.started=Time.realtimeSinceStartupAsDouble;
            runner.profile=ProfileStore.UseTransientProfile(new PlayerProfile());
            Application.logMessageReceived+=runner.Capture;
            Application.runInBackground=true;
        }
        private IEnumerator Start()
        {
            report.utc=DateTime.UtcNow.ToString("O");report.unity=Application.unityVersion;
            var routines=new Stack<IEnumerator>();routines.Push(Run());
            while(routines.Count>0&&!finished)
            {
                object current=null;bool moved;
                try{moved=routines.Peek().MoveNext();if(moved)current=routines.Peek().Current;}
                catch(Exception ex){Finish(false,ex.ToString());yield break;}
                if(!moved){(routines.Pop() as IDisposable)?.Dispose();continue;}
                var nested=current as IEnumerator;if(nested!=null){routines.Push(nested);continue;}
                yield return current;
            }
        }
        private IEnumerator Run()
        {
            yield return Wait(()=>GameRoot.Instance!=null&&GameRoot.Instance.CurrentProcedure=="ProcedureMainMenu","main menu");
            yield return Navigate(GameScene.Valley);
            var valley=FindObjectOfType<ValleyPresenter>();
            Require(valley.journal!=null&&valley.interactions.Length>=5,"Saved Stage B scene is incomplete.");
            valley.journey.OpenDialogue(2);
            Require(!valley.journey.accept.interactable&&!valley.FindInteraction("final").TryExecute(),"Fresh profile bypassed final guardian gate.");
            valley.journey.CloseDialogue();Record("saved-world-and-dialogue-gate");
            valley.journey.OpenDialogue(0);valley.journey.accept.onClick.Invoke();
            yield return Wait(()=>SceneManager.GetActiveScene().name=="GestureLab"&&!GameNavigation.IsInputBlocked,"practice dialogue action");
            yield return null;
            var practice=FindObjectOfType<PracticeProgress>();var board=practice.board;
            int initial=ProfileStore.Current.tickets;
            board.ShowSample(FixedGestureSamples.Create(board.template.ToRules())[0].Points);
            Require(ProfileStore.Current.tickets==initial&&!ProfileStore.Progress.IsClaimed("guiyun.practice"),"Program sample granted a reward.");
            var nodes=board.template.nodes;double start=Time.unscaledTimeAsDouble;
            board.Begin(new Point2(nodes[0].x,nodes[0].y),start);
            for(int i=1;i<nodes.Length-1;i++)board.Move(new Point2(nodes[i].x,nodes[i].y),start+i*.1);
            var end=nodes[nodes.Length-1];board.Finish(new Point2(end.x,end.y),start+1);
            Require(board.LastResult.Valid&&board.LastResult.Score>=90&&!board.IsProgramSample,"Real sampler failed to score the path.");
            Require(ProfileStore.Progress.IsClaimed("guiyun.practice")&&ProfileStore.Current.tickets>initial,"Practice quest failed to commit.");
            Record("practice-sampler-to-quest-reward");
            yield return Navigate(GameScene.Valley);valley=FindObjectOfType<ValleyPresenter>();
            Require(ProfileStore.Progress.GetTracked().Id=="guiyun.bamboo","Tracker did not advance after returning.");
            foreach(var cache in valley.journey.caches)Require(cache.Collect(),"Saved cache could not be collected.");
            valley.journal.Open();yield return null;
            var row=valley.journal.content.GetComponentsInChildren<QuestRowView>().Single(x=>x.QuestId=="guiyun.caches");
            Require(row.claimButton.interactable&&valley.explorer.IsInputBlocked,"Ready quest UI/modal not synchronized.");
            int fragments=ProfileStore.Current.fragments;
            row.claimButton.onClick.Invoke();
            Require(!row.claimButton.interactable&&ProfileStore.Current.fragments>fragments,"Saved claim button did not grant reward.");
            int paid=ProfileStore.Current.fragments;row.claimButton.onClick.Invoke();
            Require(ProfileStore.Current.fragments==paid,"Repeated UI click paid twice.");
            Record("saved-cache-facts-manual-claim-once");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(output),"journal.png"));
            yield return new WaitForEndOfFrame();
            valley.journal.Close();valley.ToggleInventory();
            Require(valley.inventoryPanel.activeSelf&&valley.explorer.IsInputBlocked,"Inventory failed to take input ownership.");
            string swap=ProfileStore.Current.loadout[1];valley.journey.SelectTechnique(Array.IndexOf(BattleContent.Ids,swap));
            valley.journey.equipSelected[0].onClick.Invoke();
            Require(ProfileStore.Current.loadout[0]==swap,"Saved equipment action did not swap slots.");
            valley.ToggleInventory();Require(!valley.explorer.IsInputBlocked,"Inventory did not release input.");
            string serialized=JsonUtility.ToJson(ProfileStore.Current,true);
            string save=Path.Combine(Path.GetDirectoryName(output),"isolated-profile.json");File.WriteAllText(save,serialized);
            Record("inventory-swap-and-isolated-save");
            yield return Navigate(GameScene.MainMenu);
            reloaded=ProfileStore.UseTransientProfile(JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(save)));
            yield return Navigate(GameScene.Valley);valley=FindObjectOfType<ValleyPresenter>();
            for(int i=0;i<5;i++){valley.journey.Refresh();valley.journal.Open();valley.journal.Refresh();valley.journal.Close();}
            Require(JsonUtility.ToJson(ProfileStore.Current,true)==serialized,"Scene/UI reload changed progress or paid again.");
            Require(ProfileStore.Current.loadout[0]==swap&&ProfileStore.Progress.IsClaimed("guiyun.caches"),"Save lost equipment or receipt.");
            Record("reload-no-view-side-effects");
            Require(errors.Count==0,"Unity reported errors.");Finish(true,null);
        }
        private static IEnumerator Navigate(GameScene scene)
        {
            Require(GameNavigation.GoTo(scene),"Navigation rejected: "+scene);
            yield return Wait(()=>SceneManager.GetActiveScene().name==scene.ToString()&&!GameNavigation.IsInputBlocked,"navigation "+scene);
            for(int i=0;i<3;i++)yield return null;
        }
        private static IEnumerator Wait(Func<bool> condition,string label)
        {
            double end=Time.realtimeSinceStartupAsDouble+35;
            while(!condition()){Require(Time.realtimeSinceStartupAsDouble<end,"Timed out: "+label);yield return null;}
        }
        private void Update(){if(!finished&&Time.realtimeSinceStartupAsDouble-started>180)Finish(false,"Overall timeout");}
        private void Capture(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message+"\n"+stack);}
        private void Record(string step){steps.Add(step);Write();}
        private void Write()
        {
            report.finished=finished;report.elapsedSeconds=Time.realtimeSinceStartupAsDouble-started;
            report.tickets=ProfileStore.Current.tickets;report.fragments=ProfileStore.Current.fragments;
            report.completedQuests=ProfileStore.Current.claimedQuests.Count;report.steps=steps.ToArray();
            report.errors=errors.ToArray();report.errorLogCount=errors.Count;
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
        }
        private void Finish(bool passed,string failure)
        {
            if(finished)return;finished=true;report.passed=passed;report.failure=failure;
            try{Write();}catch(Exception ex){Debug.LogError(ex);passed=false;}
            Cleanup();Debug.Log(passed?"YIBI_QUEST_SMOKE_PASS":"YIBI_QUEST_SMOKE_FAIL "+failure);
            Application.Quit(passed?0:1);
        }
        private void Cleanup(){Application.logMessageReceived-=Capture;reloaded?.Dispose();reloaded=null;profile?.Dispose();profile=null;}
        private void OnDestroy(){Cleanup();}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
