using System;
using UnityEngine;
using UnityEngine.UI;
using Yibi.App;
using Yibi.Battle;
using Yibi.Rules;

namespace Yibi.World
{
    public sealed class JourneyPresenter : MonoBehaviour
    {
        public ValleyPresenter valley;
        public Text objective,chapter,compass,treasureHint,dialogueTitle,dialogueBody,collection;
        public GameObject dialoguePanel;
        public Button accept;
        public TreasureCache[] caches;
        public RectTransform mapPlayer;
        public RectTransform[] mapTargets;
        public Vector2 mapMin=new Vector2(-30,-35),mapMax=new Vector2(70,90);
        public Image[] skillCards;
        public Text techniqueDetail;
        public Button[] equipSelected;
        public Color lockedCard=new Color(.10f,.13f,.14f),normalCard=new Color(.12f,.18f,.19f),selectedCard=new Color(.36f,.30f,.19f);
        public string completedTitle="归云篇 · 已完成";
        [TextArea] public string completedObjective="自由探索归云谷，收集秘籍，或在论武场与好友切磋。";
        private WorldInteraction pending;
        private WorldInteraction trackedTarget;
        private bool residentDialogue;
        private int selectedTechnique;
        private float nextRefresh;
        private int shownDistance=-1,shownQuadrant=-1,shownCaches=-1,shownOwned=-1,shownTechnique=-1;
        private bool[] ownedCards=new bool[0];
        private string[] equippedCards=new string[0];
        public bool IsModal=>dialoguePanel!=null&&dialoguePanel.activeSelf;

        private void OnEnable(){ProfileStore.Changed+=Refresh;}
        private void OnDisable(){ProfileStore.Changed-=Refresh;}
        private void Start(){ProfileStore.Load();dialoguePanel.SetActive(false);Refresh();}
        private void Update()
        {
            if(valley==null||valley.explorer==null)return;
            var player=valley.explorer.transform.position;Map(mapPlayer,player);
            for(int i=0;i<mapTargets.Length&&i<valley.interactionPoints.Length;i++)
                if(valley.interactionPoints[i]!=null)Map(mapTargets[i],valley.interactionPoints[i].position);
            if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.2f;RefreshDirection(player);RefreshCollection();}
            TreasureCache nearest=null;float distance=3.5f;
            foreach(var chest in caches)
            {
                if(chest==null||!chest.isActiveAndEnabled||chest.Claimed)continue;
                float d=Vector3.Distance(chest.transform.position,player);
                if(d<distance){distance=d;nearest=chest;}
            }
            string hint=nearest==null||valley.explorer.IsInputBlocked?"":"F · 开启江湖遗匣 · 获得签令";
            if(treasureHint.text!=hint)treasureHint.text=hint;
            if(nearest!=null&&!valley.explorer.IsInputBlocked&&Input.GetKeyDown(KeyCode.F))
            {
                valley.SetNotice(nearest.Collect()?"开启遗匣，获得 "+nearest.tickets+" 签令。按 J 查看收集任务。":ProfileStore.Notice);
                Refresh();
            }
        }
        private void Map(RectTransform icon,Vector3 p)
        {
            if(icon==null)return;
            icon.anchoredPosition=new Vector2((Mathf.InverseLerp(mapMin.x,mapMax.x,p.x)-.5f)*160,(Mathf.InverseLerp(mapMin.y,mapMax.y,p.z)-.5f)*130);
        }
        // Reading a view never changes progression or pays a reward.
        public void Refresh()
        {
            if(ProfileStore.Current==null||valley==null||chapter==null)return;
            var task=ProfileStore.Progress.GetTracked();
            chapter.text=task==null?completedTitle:task.Title;
            objective.text=task==null?completedObjective:task.Description+"\n"+task.ProgressText;
            trackedTarget=task==null?null:valley.FindInteraction(task.TargetId);
            shownDistance=-1;shownQuadrant=-1;
            RefreshDirection(valley.explorer.transform.position);RefreshCollection();
            if(valley.inventoryPanel.activeSelf)RefreshCards();
            if(IsModal&&pending!=null)RefreshDialogue();
        }
        private void RefreshDirection(Vector3 player)
        {
            if(trackedTarget==null){if(compass.text!="J · 查看江湖纪事")compass.text="J · 查看江湖纪事";return;}
            var delta=trackedTarget.transform.position-player;delta.y=0;
            int distance=Mathf.RoundToInt(delta.magnitude),quadrant=(delta.z>=0?2:0)+(delta.x>=0?1:0);
            if(distance==shownDistance&&quadrant==shownQuadrant)return;
            shownDistance=distance;shownQuadrant=quadrant;
            compass.text=trackedTarget.displayName+" · "+(delta.z>=0?"北":"南")+" · "+(delta.x>=0?"东":"西")+" "+distance+" 米";
        }
        private void RefreshCollection()
        {
            if(ProfileStore.Current==null||collection==null)return;
            int count=0;foreach(var cache in caches)if(cache!=null&&cache.Claimed)count++;
            int owned=ProfileStore.Current.unlocked.Count;
            if(count==shownCaches&&owned==shownOwned)return;shownCaches=count;shownOwned=owned;
            collection.text="遗匣 "+count+" / "+caches.Length+"   秘籍 "+owned+" / "+BattleContent.Ids.Length;
        }
        public void OpenDialogue(int index)
        {
            if(GameNavigation.IsInputBlocked)return;
            var point=valley.GetInteraction(index);if(point==null||point.dialogue==null)return;
            if(valley.journal!=null&&valley.journal.IsOpen)valley.journal.Close();
            valley.inventoryPanel.SetActive(false);valley.pausePanel.SetActive(false);
            pending=point;residentDialogue=false;RefreshDialogue();
            dialoguePanel.SetActive(true);valley.RefreshInputLock();
        }
        private void RefreshDialogue()
        {
            var data=pending.dialogue;
            dialogueTitle.text=data.speaker;
            accept.interactable=pending.CanExecute;
            dialogueBody.text=accept.interactable?data.body:data.blockedBody;
            accept.GetComponentInChildren<Text>().text=data.acceptLabel;
        }
        public void OpenResident(ValleyResident resident)
        {
            if(GameNavigation.IsInputBlocked||resident==null)return;
            if(valley.journal!=null&&valley.journal.IsOpen)valley.journal.Close();
            valley.inventoryPanel.SetActive(false);valley.pausePanel.SetActive(false);
            pending=null;residentDialogue=true;
            dialogueTitle.text=resident.displayName;dialogueBody.text=resident.dialogue;
            accept.interactable=true;accept.GetComponentInChildren<Text>().text="领教了";
            dialoguePanel.SetActive(true);valley.RefreshInputLock();
        }
        public void AcceptDialogue()
        {
            if(!IsModal||GameNavigation.IsInputBlocked)return;
            if(residentDialogue){CloseDialogue();return;}
            var action=pending;
            // The command rechecks requirements even if they changed while this panel was open.
            if(action==null||!action.CanExecute){if(action!=null)RefreshDialogue();return;}
            CloseDialogue();action.TryExecute();
        }
        public void CloseDialogue(){pending=null;residentDialogue=false;dialoguePanel.SetActive(false);valley.RefreshInputLock();}
        public void SelectTechnique(int index){selectedTechnique=Mathf.Clamp(index,0,BattleContent.Ids.Length-1);RefreshCards();}
        public void EquipTechnique(int slot)
        {
            if(ProfileStore.TryEquipTechnique(BattleContent.Ids[selectedTechnique],slot))
            {valley.RefreshInventory();RefreshCards();}
            else valley.SetNotice(ProfileStore.Notice);
        }
        private void RefreshCards()
        {
            if(techniqueDetail==null||ProfileStore.Current==null)return;var profile=ProfileStore.Current;
            bool changed=shownTechnique!=selectedTechnique;
            if(ownedCards.Length!=BattleContent.Ids.Length){ownedCards=new bool[BattleContent.Ids.Length];changed=true;}
            if(equippedCards.Length!=profile.loadout.Length){equippedCards=new string[profile.loadout.Length];changed=true;}
            for(int i=0;i<ownedCards.Length;i++)
            {bool owned=profile.unlocked.Contains(BattleContent.Ids[i]);if(ownedCards[i]!=owned){ownedCards[i]=owned;changed=true;}}
            for(int i=0;i<equippedCards.Length;i++)if(equippedCards[i]!=profile.loadout[i]){equippedCards[i]=profile.loadout[i];changed=true;}
            if(!changed)return;shownTechnique=selectedTechnique;
            for(int i=0;i<skillCards.Length&&i<ownedCards.Length;i++)
                if(skillCards[i]!=null)skillCards[i].color=!ownedCards[i]?lockedCard:i==selectedTechnique?selectedCard:normalCard;
            string id=BattleContent.Ids[selectedTechnique];
            techniqueDetail.text=BattleContent.Name(id)+" · 内力 "+BattleContent.Costs[selectedTechnique]+"\n"+TechniqueHints.Describe(id)+"\n"+
                (profile.unlocked.Contains(id)?"已获得 · 选择装配槽（已装备秘籍会交换槽位）":"未获得 · 消耗签令可抽取未拥有秘籍");
            for(int i=0;i<equipSelected.Length&&i<profile.loadout.Length;i++)
            {
                equipSelected[i].interactable=profile.unlocked.Contains(id);
                equipSelected[i].GetComponentInChildren<Text>().text="槽 "+(i+1)+" · "+BattleContent.Name(profile.loadout[i]);
            }
        }
    }
}

