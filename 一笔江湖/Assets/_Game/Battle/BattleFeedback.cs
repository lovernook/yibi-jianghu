using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Rules;
using Yibi.Presentation;
namespace Yibi.Battle
{
    public sealed class BattleFeedback:MonoBehaviour
    {
        public BattlePresenter battle;
        public Image[] healthFills,energyFills;
        public Text[] floating;
        public Text intention,description;
        public SequenceEffect[] attackEffects, supportEffects;
        private readonly float[] visibleUntil=new float[2];
        private CharacterMotion[] actors;
        private BattleState displayed;
        private void Awake(){actors=new[]{battle.leftActor.GetComponentInChildren<CharacterMotion>(),battle.rightActor.GetComponentInChildren<CharacterMotion>()};}
        private void Update()
        {
            if(battle.State==null)return;
            bool showHints=battle.hud==null&&!battle.drawingPanel.activeSelf&&!battle.resultPanel.activeSelf;intention.gameObject.SetActive(showHints);description.gameObject.SetActive(showHints);
            for(int i=0;i<2;i++){
                healthFills[i].rectTransform.localScale=new Vector3(Mathf.Lerp(healthFills[i].rectTransform.localScale.x,battle.State.fighters[i].hp/100f,Time.unscaledDeltaTime*10),1,1);
                energyFills[i].rectTransform.localScale=new Vector3(battle.State.fighters[i].energy/6f,1,1);
                if(Time.unscaledTime>visibleUntil[i])floating[i].text="";
            }
            if(displayed!=battle.State){displayed=battle.State;if(battle.hud==null)intention.text=battle.IsOnline?"双方行动以服务端结算为准":battle.State.winner!=-2?"本场结束":battle.State.activeSeat==1?"守卫正在运功…":"对手倾向 · "+TechniqueHints.Describe(battle.PredictNpcSkill());}
        }
        public void ResetView(){StopAllCoroutines();StopEffects(attackEffects);StopEffects(supportEffects);displayed=null;for(int i=0;i<2;i++){if(actors[i]!=null)actors[i].ResetPose();floating[i].text="";healthFills[i].rectTransform.localScale=Vector3.one;}description.text="点穴 → 裂石：破绽联动    回风 → 流火：灼伤联动";}
        static void StopEffects(SequenceEffect[] effects){if(effects!=null)foreach(var effect in effects)if(effect!=null)effect.Stop();}
        public void Describe(string id){description.text=battle.TechniqueTitle(id)+" · "+battle.TechniqueDescription(id);}
        public void Transition(BattleState before,BattleState after,int seat,string id)
        {
            StopAllCoroutines();bool support=BattlePresentation.IsSupportAction(before,after,seat,id);if(actors[seat]!=null)actors[seat].PlayAction(support?"Cast":"Attack");
            var effects=support?supportEffects:attackEffects;if(effects!=null&&seat>=0&&seat<effects.Length&&effects[seat]!=null)effects[seat].Play();
            StartCoroutine(Impact(before,after));
        }
        private IEnumerator Impact(BattleState before,BattleState after)
        {
            yield return new WaitForSecondsRealtime(battle.presentation==null ? .28f : Mathf.Max(0,battle.presentation.impactDelay));
            for(int i=0;i<2;i++){
                int hp=after.fighters[i].hp-before.fighters[i].hp;int shield=after.fighters[i].shield-before.fighters[i].shield;
                if(hp<0||shield<0){if(actors[i]!=null&&(hp<0||i!=before.activeSeat))actors[i].PlayAction("Hit");floating[i].text=hp<0?hp+" 生命":"护盾 -"+(-shield);floating[i].color=new Color(1,.45f,.32f);}
                else if(hp>0||shield>0){floating[i].text=hp>0?"+"+hp+" 生命":"+"+shield+" 护盾";floating[i].color=new Color(.4f,1,.7f);}
                visibleUntil[i]=Time.unscaledTime+(battle.presentation==null?1.2f:Mathf.Max(.1f,battle.presentation.floatDuration));
            }
            if(after.winner!=-2){yield return new WaitForSecondsRealtime(battle.presentation==null ? .55f : Mathf.Max(0,battle.presentation.victoryDelay));for(int i=0;i<2;i++)if(actors[i]!=null)actors[i].PlayAction(after.winner==i?"Victory":after.winner==-1?"Cast":"Defeat");}
        }
    }
}
