using UnityEngine;
using UnityEngine.Events;
using Yibi.App;
using Yibi.Battle;

namespace Yibi.World
{
    // A scene binding holds scene references; shared content assets hold prose and requirements.
    [DisallowMultipleComponent]
    public sealed class WorldInteraction : MonoBehaviour
    {
        public string stableId;
        public string displayName;
        [Min(.1f)] public float radius=5;
        public DialogueDefinitionSO dialogue;
        public UnityEvent onAccepted=new UnityEvent();

        public bool CanExecute
        {
            get
            {
                if(dialogue==null)return true;
                var required=dialogue.requiredQuestIds;
                if(required==null)return true;
                foreach(var id in required)
                    if(string.IsNullOrWhiteSpace(id)||!ProfileStore.Progress.IsClaimed(id))return false;
                return true;
            }
        }

        public bool TryExecute()
        {
            if(GameNavigation.IsInputBlocked||!CanExecute)return false;
            onAccepted.Invoke();
            return true;
        }

        public void Execute(){TryExecute();}
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(.8f,.67f,.32f,.7f);
            Gizmos.DrawWireSphere(transform.position,radius);
        }
    }
}
