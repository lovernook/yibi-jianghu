using System;
using UnityEngine;
using UnityEngine.UI;
using Yibi.App;
using Yibi.Battle;
using Yibi.Rules;

namespace Yibi.World
{
    public sealed class ValleyPresenter : MonoBehaviour
    {
        public ValleyExplorer explorer;
        // Compatibility references for older scene tools; behavior uses the scene bindings.
        public Transform[] interactionPoints,checkpoints;
        public string[] interactionNames;
        public WorldInteraction[] interactions;
        public Text prompt,region,notice,profileText,trialText,inventoryNotice;
        public GameObject inventoryPanel,pausePanel;
        public Button[] equipButtons;
        public Text mindsetLabel;
        public JourneyPresenter journey;
        public QuestJournalPresenter journal;
        [Min(1)] public float trialDuration=45;
        [Min(.1f)] public float checkpointRadius=3;
        public string trialFact="trial.complete";
        private int nearest=-1,nextCheckpoint=-1;
        private float trialStart;
        private ValleyResident[] residents;
        private int shownNearest=-2,shownTenth=-1;
        private ValleyResident shownResident;
        private bool shownLock;
        private string pendingTrialFact;
        private string message="WASD 行走 · Shift 奔跑 · E 交谈 · F 开匣 · Tab 行囊 · J 任务";

        public bool IsModalOpen => inventoryPanel.activeSelf || pausePanel.activeSelf
            || (journey!=null&&journey.IsModal) || (journal!=null&&journal.IsOpen);
        public bool TrialActive => nextCheckpoint>=0;
        public bool HasPendingTrial => !string.IsNullOrEmpty(pendingTrialFact);
        public int NextCheckpoint => nextCheckpoint;
        private void OnEnable(){ProfileStore.Changed+=RefreshProfile;}
        private void OnDisable(){ProfileStore.Changed-=RefreshProfile;}
        private void Start()
        {
            residents=FindObjectsOfType<ValleyResident>();ProfileStore.Load();
            if(ProfileStore.HasReturnPosition){var c=explorer.GetComponent<CharacterController>();c.enabled=false;explorer.transform.position=ProfileStore.ReturnPosition;c.enabled=true;}
            inventoryPanel.SetActive(false);pausePanel.SetActive(false);RefreshProfile();RefreshInputLock();
            if(!string.IsNullOrEmpty(ProfileStore.Notice))message=ProfileStore.Notice;
        }
        private void Update()
        {
            if(!GameNavigation.IsInputBlocked)
            {
                if(Input.GetKeyDown(KeyCode.Tab))ToggleInventory();
                if(Input.GetKeyDown(KeyCode.J)&&journal!=null)journal.Toggle();
                if(Input.GetKeyDown(KeyCode.Escape))
                {
                    if(journey!=null&&journey.IsModal)journey.CloseDialogue();
                    else if(inventoryPanel.activeSelf)inventoryPanel.SetActive(false);
                    else if(journal!=null&&journal.IsOpen)journal.Close();
                    else pausePanel.SetActive(!pausePanel.activeSelf);
                }
            }
            RefreshInputLock();nearest=-1;float closest=float.MaxValue;
            if(interactions!=null)for(int i=0;i<interactions.Length;i++)
            {
                var point=interactions[i];if(point==null||!point.isActiveAndEnabled)continue;
                float d=Vector3.Distance(explorer.transform.position,point.transform.position);
                if(d<point.radius&&d<closest){closest=d;nearest=i;}
            }
            ValleyResident resident=null;
            foreach(var person in residents)
            {
                if(person==null||!person.isActiveAndEnabled)continue;
                float d=Vector3.Distance(person.transform.position,explorer.transform.position);
                if(d<person.radius&&d<closest){closest=d;resident=person;}
            }
            if(shownNearest!=nearest||shownResident!=resident||shownLock!=explorer.IsInputBlocked)
            {
                shownNearest=nearest;shownResident=resident;shownLock=explorer.IsInputBlocked;
                prompt.text=shownLock?"":resident!=null?"E · "+resident.displayName:nearest<0?"沿山道寻找练功台、竹林和遗迹":"E · "+interactions[nearest].displayName;
            }
            region.text=explorer.transform.position.z>30?"遗迹 · 问心台":explorer.transform.position.x>20?"竹林 · 听风径":"村落 · 归云谷";
            if(notice.text!=message)notice.text=message;
            if(Input.GetKeyDown(KeyCode.E)&&!explorer.IsInputBlocked)
            {if(resident!=null&&journey!=null)journey.OpenResident(resident);else if(nearest>=0)Interact(nearest);}
            UpdateTrial();
        }
        private void UpdateTrial()
        {
            if(nextCheckpoint<0)
            {
                string status=HasPendingTrial?"试炼已完成 · 等待保存，返回听风碑重试":"";
                if(trialText.text!=status)trialText.text=status;
                return;
            }
            float elapsed=Time.unscaledTime-trialStart;int tenth=(int)(elapsed*10);
            if(tenth!=shownTenth){shownTenth=tenth;trialText.text="疾行试炼 · "+(nextCheckpoint+1)+" / "+checkpoints.Length+"   "+elapsed.ToString("F1")+" / "+trialDuration.ToString("0")+" 秒";}
            if(elapsed>trialDuration){nextCheckpoint=-1;message="试炼超时，返回起点可重试。";MarkCheckpoint();return;}
            if(Vector3.Distance(explorer.transform.position,checkpoints[nextCheckpoint].position)>=checkpointRadius)return;
            nextCheckpoint++;
            if(nextCheckpoint>=checkpoints.Length)
            {
                nextCheckpoint=-1;pendingTrialFact=trialFact;RetryPendingTrial();
            }
            MarkCheckpoint();
        }
        public WorldInteraction GetInteraction(int index)=>interactions!=null&&index>=0&&index<interactions.Length?interactions[index]:null;
        public WorldInteraction FindInteraction(string id)
        {if(interactions!=null)foreach(var point in interactions)if(point!=null&&point.stableId==id)return point;return null;}
        public void Interact(int index)
        {
            if(GameNavigation.IsInputBlocked)return;var point=GetInteraction(index);if(point==null)return;
            if(point.dialogue!=null&&journey!=null)journey.OpenDialogue(index);else point.TryExecute();
        }
        // Keeps saved integer callbacks usable without an index-to-gameplay switch.
        public void PerformInteraction(int index){GetInteraction(index)?.TryExecute();}
        private void RememberPosition(){ProfileStore.ReturnPosition=explorer.transform.position;ProfileStore.HasReturnPosition=true;}
        public void GoToPractice(){if(!RetryPendingTrial())return;RememberPosition();GameNavigation.GoTo(GameScene.GestureLab);}
        public void ChallengeBamboo(){if(!RetryPendingTrial())return;RememberPosition();ProfileStore.NpcStrategy=0;GameNavigation.GoTo(GameScene.Arena_Stone);}
        public void ChallengeFinal(){if(!RetryPendingTrial())return;RememberPosition();ProfileStore.NpcStrategy=1;GameNavigation.GoTo(GameScene.Arena_Bamboo);}
        public void StartTrial()
        {
            if(HasPendingTrial){RetryPendingTrial();return;}
            if(checkpoints==null||checkpoints.Length==0){SetNotice("试炼路线尚未配置。");return;}
            nextCheckpoint=0;trialStart=Time.unscaledTime;shownTenth=-1;MarkCheckpoint();
            message="依次穿过亮起的标记，在 "+trialDuration.ToString("0")+" 秒内完成。";
        }
        // A completed run is retained in memory until a deliberate retry/leave action can commit it.
        // UI refresh and Update do not repeatedly attempt writes after a failed completion.
        public bool RetryPendingTrial()
        {
            if(!HasPendingTrial)return true;
            bool alreadyRecorded=ProfileStore.Current.questFacts.Exists(f=>f.key==pendingTrialFact&&f.value>=1);
            if(alreadyRecorded||ProfileStore.RecordFact(pendingTrialFact,1))
            {
                pendingTrialFact=null;
                SetNotice("疾行试炼完成 · 任务进度已保存，可按 J 查看奖励。");
                return true;
            }
            SetNotice(ProfileStore.Notice+" 本次试炼结果仍保留；返回听风碑或再次点击离开即可重试。");
            return false;
        }
        private void MarkCheckpoint(){for(int i=0;i<checkpoints.Length;i++)if(checkpoints[i]!=null)checkpoints[i].gameObject.SetActive(nextCheckpoint==i);}
        public void RefreshInputLock(){if(explorer!=null)explorer.inputLocked=GameNavigation.IsInputBlocked||IsModalOpen;}
        public void ToggleInventory()
        {
            if(GameNavigation.IsInputBlocked)return;
            if(journey!=null&&journey.IsModal)journey.CloseDialogue();
            if(journal!=null&&journal.IsOpen)journal.Close();
            pausePanel.SetActive(false);inventoryPanel.SetActive(!inventoryPanel.activeSelf);
            if(inventoryPanel.activeSelf&&inventoryNotice!=null)inventoryNotice.text="";
            RefreshProfile();if(inventoryPanel.activeSelf&&journey!=null)journey.Refresh();RefreshInputLock();
        }
        public void SetNotice(string text)
        {
            message=text??"";
            if(notice!=null)notice.text=message;
            if(inventoryNotice!=null)inventoryNotice.text=message;
        }
        public void RefreshInventory(){RefreshProfile();}
        public void Resume(){pausePanel.SetActive(false);RefreshInputLock();}
        public void OpenLobby(){if(!RetryPendingTrial())return;pausePanel.SetActive(false);GameNavigation.GoTo(GameScene.Lobby);}
        public void Quit(){if(TrySaveBeforeLeaving())Application.Quit();}
        public void ReturnToTitle(){if(!TrySaveBeforeLeaving())return;pausePanel.SetActive(false);GameNavigation.GoTo(GameScene.MainMenu);}
        private bool TrySaveBeforeLeaving()
        {
            if(!RetryPendingTrial())return false;
            try{ProfileStore.Save();return true;}
            catch(Exception){SetNotice("保存失败，仍留在当前场景。请确认存档目录可写后重试。");return false;}
        }
        public void Draw(){SetNotice(ProfileStore.Draw(UnityEngine.Random.Range(0,int.MaxValue)));}
        public void Equip(int slot)
        {
            var p=ProfileStore.Current;if(p==null||slot<0||slot>=p.loadout.Length)return;
            int start=Array.IndexOf(BattleContent.Ids,p.loadout[slot]);
            for(int n=1;n<=BattleContent.Ids.Length;n++)
            {
                string id=BattleContent.Ids[(start+n)%BattleContent.Ids.Length];
                if(p.unlocked.Contains(id)&&Array.IndexOf(p.loadout,id)<0)
                {if(!ProfileStore.TryEquipTechnique(id,slot))SetNotice(ProfileStore.Notice);break;}
            }
        }
        public void SwitchMindset()
        {
            if(!ProfileStore.TrySetMindset(ProfileStore.Current.mindset=="shouzhuo"?"fanzhao":"shouzhuo"))SetNotice(ProfileStore.Notice);
        }
        private void RefreshProfile()
        {
            var p=ProfileStore.Current;if(p==null||profileText==null)return;
            profileText.text="签令 "+p.tickets+"    碎片 "+p.fragments+"    已藏秘籍 "+p.unlocked.Count+" / "+BattleContent.Ids.Length;
            for(int i=0;i<equipButtons.Length&&i<p.loadout.Length;i++)if(equipButtons[i]!=null)equipButtons[i].GetComponentInChildren<Text>().text="槽 "+(i+1)+" · "+BattleContent.Name(p.loadout[i]);
            mindsetLabel.text=p.mindset=="shouzhuo"?"守拙 · 调息额外获得护盾":"反照 · 护盾受击触发反击";
        }
    }
}

