using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yibi.Rules;
using Yibi.UI;
using Yibi.Networking;

namespace Yibi.Battle
{
    public sealed class BattlePresenter:MonoBehaviour
    {
        public GestureBoard board;
        public GestureTemplateSO[] routes;
        public Text leftStatus,rightStatus,turnLabel,scoreLabel,logLabel,resultLabel;
        public Button[] skillButtons;
        public Sprite[] techniqueIcons;
        public Image[] skillIcons;
        public Button basicButton,restButton,confirmButton;
        public GameObject drawingPanel,resultPanel;
        public Transform leftActor,rightActor;
        public BattleFeedback feedback;
        public BattlePresentationProfile presentation;
        public NpcTacticProfile[] npcTactics;
        public BattleHUDView hud;
        public event Action Changed;
        public bool IsOnline=>online;
        public string SelectedSkill=>selected;
        public int PlayerSeat=>LocalSeat;
        public bool IsResolving=>resolving;
        public string LastResolution{get;private set;}
        public BattleState State{get;private set;}
        public int npcStrategy;
        private string selected;
        private double deadline,nextNpcAt;
        private bool resolving;
        private bool online;
        private string lastNetStatus;
        private string pendingVictoryFact;
        public bool HasPendingVictory=>!string.IsNullOrEmpty(pendingVictoryFact);
        private int LocalSeat=>online?NetSession.Instance.Seat:0;
        private readonly System.Collections.Generic.Queue<string> history=new System.Collections.Generic.Queue<string>();
        private Vector3 leftHome,rightHome;
        private bool initialized;
        private int shownSecond=-1,shownTurn=-1;
        private bool CanLocalAct=>State!=null&&State.winner==-2&&State.activeSeat==LocalSeat&&!resolving&&(!online||NetSession.Instance.CanAct);
        private void Start(){board.Scored+=OnScored;board.Changed+=OnBoardChanged;Restart();}
        private void OnDestroy(){if(board!=null){board.Scored-=OnScored;board.Changed-=OnBoardChanged;}}
        public string TechniqueTitle(string id){var entry=presentation==null?null:presentation.Find(id);return entry==null||string.IsNullOrEmpty(entry.title)?BattleContent.Name(id):entry.title;}
        public string TechniqueDescription(string id){var entry=presentation==null?null:presentation.Find(id);return entry==null?TechniqueHints.Describe(id):entry.description;}
        public string TechniqueHint(string id){var entry=presentation==null?null:presentation.Find(id);return entry==null?"注意内力、冷却和对手状态。":entry.tacticalHint;}
        public void Restart()
        {
            if(!online&&!CommitPendingVictory()){Refresh();return;}
            if(online&&NetSession.Instance.Aborted){NetSession.Instance.Leave();SceneManager.LoadScene("Lobby");return;}
            if(online&&State!=null&&State.winner!=-2){NetSession.Instance.Rematch();return;}
            if(!initialized){leftHome=leftActor.localPosition;rightHome=rightActor.localPosition;initialized=true;}
            StopAllCoroutines();leftActor.localPosition=leftHome;rightActor.localPosition=rightHome;resolving=false;selected=null;LastResolution=null;history.Clear();if(feedback!=null)feedback.ResetView();
            online=NetSession.Instance!=null&&NetSession.Instance.State!=null;
            if(online){State=NetSession.Instance.State;deadline=NetSession.Instance.Deadline;drawingPanel.SetActive(false);resultPanel.SetActive(false);Refresh();return;}
            ProfileStore.Load();npcStrategy=ProfileStore.NpcStrategy;
            State=BattleReducer.Create(Guid.NewGuid().ToString("N"),0,ProfileStore.Current.loadout,npcStrategy==1?new[]{"huifeng","liuhuo","qingxin"}:null,ProfileStore.Current.mindset);
            resultPanel.SetActive(false);drawingPanel.SetActive(false);deadline=Time.unscaledTimeAsDouble+25;nextNpcAt=0;Refresh();
        }
        private void Update()
        {
            if(online){
                var net=NetSession.Instance;
                if(net.Aborted){drawingPanel.SetActive(false);board.inputEnabled=false;resultPanel.SetActive(true);resultLabel.text="对局中止 · 不记胜负";turnLabel.text=net.Status;basicButton.interactable=restButton.interactable=confirmButton.interactable=false;foreach(var b in skillButtons)b.interactable=false;return;}
                if(net.Synchronized&&net.Seat>=0&&net.State==null){SceneManager.LoadScene("Lobby");return;}
                if(net.Seat<0)return;
                if(lastNetStatus!=net.Status){lastNetStatus=net.Status;shownSecond=-1;Refresh();if(net.CanAct&&drawingPanel.activeSelf){board.inputEnabled=true;OnBoardChanged();}}
                if(net.State!=null&&(State!=net.State)){
                    var previous=State;State=net.State;deadline=net.Deadline;shownSecond=-1;selected=null;drawingPanel.SetActive(false);board.inputEnabled=false;
                    if(previous==null||previous.matchId!=State.matchId){history.Clear();LastResolution=null;resultPanel.SetActive(false);if(feedback!=null)feedback.ResetView();}
                    else if(State.revision>previous.revision){LastResolution=BattlePresentation.Settlement(previous,State,net.LastEvents,LocalSeat);if(State.revision==previous.revision+1&&net.LastEvents.Length>0){if(feedback!=null)feedback.Transition(previous,State,previous.activeSeat,SkillFromEvents(net.LastEvents));}else LastResolution="已同步权威状态；未回放缺失的行动。\n"+LastResolution;}
                    foreach(var line in net.LastEvents){history.Enqueue(BattlePresentation.Perspective(line,LocalSeat));if(history.Count>7)history.Dequeue();}Refresh();
                }
                if(!net.CanAct){board.inputEnabled=false;confirmButton.interactable=false;if(net.Recovering)drawingPanel.SetActive(false);turnLabel.text=net.Status;return;}
                deadline=net.Deadline;
            }
            if(State==null||State.winner!=-2||resolving)return;
            int seconds=Math.Max(0,(int)Math.Ceiling(deadline-Time.unscaledTimeAsDouble));if(seconds!=shownSecond||shownTurn!=State.turnId){shownSecond=seconds;shownTurn=State.turnId;turnLabel.text="第 "+((State.turnId+1)/2)+" / 20 轮   ·   "+(State.activeSeat==LocalSeat?"你的回合":"对手回合")+"   ·   "+seconds+" 秒";}
            if(online)return;
            if(State.activeSeat==1&&Time.unscaledTimeAsDouble>=nextNpcAt){NpcAct();return;}
            if(Time.unscaledTimeAsDouble>=deadline)Resolve("rest",null,true);
        }
        public void SelectSkill(int slot)
        {
            if(!CanLocalAct||slot<0||slot>=State.fighters[LocalSeat].loadout.Length)return;
            var id=State.fighters[LocalSeat].loadout[slot];var error=BattleReducer.CanUse(State,LocalSeat,id);if(error!=null){scoreLabel.text=error;return;}
            int routeIndex=Array.IndexOf(BattleContent.Ids,id);if(routes==null||routeIndex<0||routeIndex>=routes.Length||routes[routeIndex]==null){scoreLabel.text="秘籍路线未配置，请检查战斗配置。";return;}
            selected=id;drawingPanel.SetActive(true);board.SetTemplate(routes[routeIndex]);board.inputEnabled=true;
            scoreLabel.text=BattleContent.Name(id)+" · 按顺序连通穴位，松开后确认";confirmButton.interactable=false;
            if(feedback!=null)feedback.Describe(id);
            Changed?.Invoke();
        }
        public void Basic(){if(CanLocalAct)Resolve("basic");}
        public void Rest(){if(CanLocalAct)Resolve("rest");}
        public void Clear(){board.Clear();confirmButton.interactable=false;scoreLabel.text="重新绘制，不重置本回合计时";}
        public void Confirm(){if(!CanLocalAct||selected==null||board.LastResult==null||board.Sampler.IsDrawing)return;if(board.IsProgramSample){scoreLabel.text="程序样本不能出招，请重新用鼠标起笔。";confirmButton.interactable=false;Changed?.Invoke();return;}Resolve(selected,board.Sampler.Snapshot());}
        private void OnBoardChanged(){confirmButton.interactable=CanLocalAct&&selected!=null&&board.LastResult!=null&&!board.IsProgramSample&&!board.Sampler.IsDrawing;}
        private void OnScored(ScoreResult r){scoreLabel.text=board.IsProgramSample?"程序样本不能出招，请重新用鼠标起笔。":r.Valid?BattlePresentation.Grade(r):BattlePresentation.Recovery(r);OnBoardChanged();Changed?.Invoke();}
        private void NpcAct()
        {
            string id=PredictNpcSkill();Resolve(id,id=="basic"||id=="rest"?null:BattleContent.Perfect(id));
        }
        public string PredictNpcSkill()
        {
            if(npcTactics!=null&&npcStrategy>=0&&npcStrategy<npcTactics.Length&&npcTactics[npcStrategy]!=null)return npcTactics[npcStrategy].Select(State);
            return NpcTacticProfile.SelectDefault(State,npcStrategy);
        }
        public void Resolve(string id,QuantizedPoint[] points=null,bool timeout=false)
        {
            if(State==null||State.winner!=-2||resolving)return;
            if(online){if(!NetSession.Instance.CanAct)return;NetSession.Instance.Submit(id,points,selected!=null&&board.Sampler.OutOfBounds,selected!=null&&board.Sampler.Overflow);board.inputEnabled=false;confirmButton.interactable=false;Refresh();return;}
            var transition=BattleReducer.Apply(State,new BattleCommand{commandId=Guid.NewGuid().ToString("N"),matchId=State.matchId,turnId=State.turnId,seat=State.activeSeat,skillId=id,points=points??Array.Empty<QuantizedPoint>(),timeout=timeout,outOfBounds=State.activeSeat==0&&selected!=null&&board.Sampler.OutOfBounds,overflow=State.activeSeat==0&&selected!=null&&board.Sampler.Overflow});
            if(!transition.Accepted){scoreLabel.text=transition.rejection;return;}
            int actor=State.activeSeat;var before=State;State=transition.state;
            // A fact is reported once at the authoritative offline victory transition.
            // Presentation refreshes never pay rewards, and online matches use their own path above.
            if(before.winner==-2&&State.winner==0){pendingVictoryFact="win."+npcStrategy;CommitPendingVictory();}
            string displayedAction=transition.score!=null&&!transition.score.Valid?"basic":id;
            if(feedback!=null)feedback.Transition(before,State,actor,displayedAction);
            LastResolution=BattlePresentation.Settlement(before,State,transition.events,LocalSeat);
            foreach(var line in transition.events){history.Enqueue(line);if(history.Count>7)history.Dequeue();}
            selected=null;drawingPanel.SetActive(false);board.inputEnabled=false;resolving=true;Refresh();StartCoroutine(Present(actor,displayedAction));
        }
        private IEnumerator Present(int actor,string id)
        {
            var t=actor==0?leftActor:rightActor;var origin=t.localPosition;
            float duration=presentation==null ? .9f : Mathf.Max(.05f,presentation.actionDuration);
            float advance=BattlePresentation.IsSupportAction(null,null,actor,id)?0:.65f;
            for(float time=0;time<duration;time+=Time.unscaledDeltaTime){t.localPosition=origin+Vector3.right*(actor==0?1:-1)*Mathf.Sin(time/duration*Mathf.PI)*advance;yield return null;}
            t.localPosition=origin;resolving=false;deadline=Time.unscaledTimeAsDouble+25;nextNpcAt=Time.unscaledTimeAsDouble+(presentation==null?1.2f:Mathf.Max(0,presentation.npcThinkDelay));Refresh();
        }
        private static string SkillFromEvents(string[] events){if(events!=null)foreach(var line in events){if(line.Contains("运功失误，改为吐纳掌"))return "basic";foreach(var id in BattleContent.Ids)if(line.Contains(" · "+BattleContent.Name(id)))return id;if(line.Contains(" · 调息"))return "rest";if(line.Contains(" · 吐纳掌"))return "basic";}return "network";}
        private static string Status(FighterState f,string label)=>label+"\n生命 "+f.hp+" / 100    内力 "+f.energy+" / 6\n护盾 "+f.shield+(f.flawUntil>=0?"   破绽":"")+(f.windUntil>=0?"   蓄风":"")+(f.burn>0?"   灼伤×"+f.burn:"")+(f.counterUntil>=0?"   反击":"");
        public void Refresh()
        {
            if(State==null||LocalSeat<0)return;
            leftStatus.text=Status(State.fighters[0],online?(LocalSeat==0?"席位一 · 你":"席位一 · 对手"):"行者 · "+(State.fighters[0].mindset=="shouzhuo"?"守拙":"反照"));rightStatus.text=Status(State.fighters[1],online?(LocalSeat==1?"席位二 · 你":"席位二 · 对手"):"试炼守卫 · 反照");logLabel.text=string.Join("\n",history);
            bool can=State.activeSeat==LocalSeat&&State.winner==-2&&!resolving&&(!online||NetSession.Instance.CanAct);
            basicButton.interactable=restButton.interactable=can;
            for(int i=0;i<skillButtons.Length;i++){var id=State.fighters[LocalSeat].loadout[i];var error=BattleReducer.CanUse(State,LocalSeat,id);skillButtons[i].interactable=can&&error==null;skillButtons[i].GetComponentInChildren<Text>().text=BattleContent.Name(id)+"\n内力 "+BattleContent.Costs[Array.IndexOf(BattleContent.Ids,id)]+(error==null?"":" · "+error);}
            if(skillIcons!=null&&techniqueIcons!=null&&skillIcons.Length==3&&techniqueIcons.Length==6)for(int i=0;i<3;i++)skillIcons[i].sprite=techniqueIcons[Array.IndexOf(BattleContent.Ids,State.fighters[LocalSeat].loadout[i])];
            if(State.winner!=-2&&!resolving)
            {
                resultPanel.SetActive(true);
                resultLabel.text=State.winner==-1?"平局 · 再论高下":State.winner==LocalSeat?"胜 · 初窥门径":"败 · 再接再厉";
                if(HasPendingVictory)resultLabel.text+="\n战果待保存";
                turnLabel.text=HasPendingVictory?"保存失败 · 确认存档目录可写后，点击返回或再战重试":"本场结束";
            }
            OnBoardChanged();Changed?.Invoke();
        }
        public bool RetryPendingVictory(){bool saved=CommitPendingVictory();Refresh();return saved;}
        private bool CommitPendingVictory()
        {
            if(!HasPendingVictory)return true;
            bool alreadyRecorded=ProfileStore.Current.questFacts.Exists(f=>f.key==pendingVictoryFact&&f.value>=1);
            if(!alreadyRecorded&&!ProfileStore.RecordFact(pendingVictoryFact,1))return false;
            pendingVictoryFact=null;return true;
        }
        public void ReturnToPractice()
        {
            if(!online&&!CommitPendingVictory()){Refresh();return;}
            if(online)NetSession.Instance.Leave();
            SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded("Valley")?"Valley":"GestureLab");
        }
        public void ResumeConnection(){if(online)NetSession.Instance.Resume();}
    }
}
