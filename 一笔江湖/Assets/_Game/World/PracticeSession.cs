using UnityEngine;
using UnityEngine.UI;
using Yibi.UI;
using Yibi.Rules;
using Yibi.Battle;

namespace Yibi.World
{
    [DisallowMultipleComponent]
    public sealed class PracticeSession : MonoBehaviour
    {
        public GestureLabPresenter lab;
        public Text history,examStatus;
        public Button examButton,nextButton;
        public Button[] routeButtons;
        [Range(1,100)] public int examPassScore=80;
        public string examQuestId="guiyun.exam";
        public string examCompletionFact="exam.complete";
        public int ExamStep {get;private set;}=-1;
        public bool AwaitingNext {get;private set;}
        private int RouteCount=>lab!=null&&lab.routes!=null?lab.routes.Length:0;
        public bool ExamActive=>ExamStep>=0&&ExamStep<RouteCount;
        string observedRoute;
        void OnEnable(){lab.board.Scored+=Scored;}
        void OnDisable(){lab.board.Scored-=Scored;}
        void Start(){ProfileStore.Load();Refresh();}
        void Update(){if(observedRoute!=lab.board.template.stableId)Refresh();}
        public void ToggleExam()
        {
            if(ExamActive){ExamStep=-1;AwaitingNext=false;examStatus.text="已退出考核 · 练习记录仍保留";}
            else if(RouteCount>0){ExamStep=0;AwaitingNext=false;lab.SelectRoute(0);examStatus.text="经脉考核 1 / "+RouteCount+" · 本笔达到 "+examPassScore+" 分后进入下一题";}
            Refresh();
        }
        public void Next()
        {
            if(!ExamActive||!AwaitingNext)return;
            if(ExamStep+1==RouteCount)
            {
                bool alreadyRecorded=ProfileStore.Current.questFacts.Exists(f=>f.key==examCompletionFact&&f.value>=1);
                if(!ProfileStore.RecordFact(examCompletionFact,1)&&!alreadyRecorded)
                {
                    examStatus.text=ProfileStore.Notice+"\n本题成绩已保留，可再次点击完成考核。";
                    Refresh();return;
                }
                AwaitingNext=false;ExamStep=RouteCount;
                examStatus.text="经脉贯通 · 考核完成！";
                foreach(var quest in ProfileStore.Progress.GetSnapshots())
                    if(quest.Id==examQuestId)
                    {
                        examStatus.text+="\n奖励 · "+quest.RewardText+(quest.State==QuestState.Claimed?" · 已领取":" · 请在任务日志查看");
                        break;
                    }
            }
            else {AwaitingNext=false;ExamStep++;lab.SelectRoute(ExamStep);examStatus.text="经脉考核 "+(ExamStep+1)+" / "+RouteCount+" · 达到 "+examPassScore+" 分可过关";}
            Refresh();
        }
        void Scored(ScoreResult score)
        {
            if(lab.board.IsProgramSample){if(ExamActive)examStatus.text="程序样本不计入考核，请亲手绘制。";return;}
            if(!ProfileStore.RecordPractice(lab.board.template.stableId,score.Valid?score.Score:0))
            {
                examStatus.text=ProfileStore.Notice+"\n本笔未计入记录，请重试。";
                Refresh();return;
            }
            if(ExamActive&&!AwaitingNext&&lab.board.template.stableId==lab.routes[ExamStep].stableId){
                AwaitingNext=score.Valid&&score.Score>=examPassScore;
                examStatus.text=AwaitingNext?"本题通过 · 点击“下一题”继续":"未达 "+examPassScore+" 分 · 可以看示范并在本题重试";
            }
            Refresh();
        }
        public void Refresh()
        {
            if(ProfileStore.Current==null||lab.board.template==null)return;observedRoute=lab.board.template.stableId;
            var record=ProfileStore.Current.practice.Find(r=>r.routeId==observedRoute);
            history.text=record==null?"此经脉尚无手绘记录":"本经脉最佳 "+record.bestScore+" 分 · 练习 "+record.attempts+" 次";
            if(!ProfileStore.Current.unlocked.Contains(observedRoute))history.text+="\n可自由试学 · 获得秘籍后可用于 PVE";
            for(int i=0;i<routeButtons.Length;i++)routeButtons[i].interactable=!ExamActive||i==ExamStep;
            examButton.GetComponentInChildren<Text>().text=ExamActive?"退出考核":"经脉考核";
            nextButton.interactable=ExamActive&&AwaitingNext;nextButton.GetComponentInChildren<Text>().text=ExamStep==RouteCount-1?"完成考核":"下一题";
        }
    }
}
