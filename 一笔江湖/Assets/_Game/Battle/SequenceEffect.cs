using UnityEngine;
namespace Yibi.Battle
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SequenceEffect:MonoBehaviour
    {
        public Texture2D[] frames;
        public float framesPerSecond=12;
        private float elapsed;
        private bool playing;
        private MeshRenderer view;
        private MaterialPropertyBlock block;
        private void Awake(){view=GetComponent<MeshRenderer>();block=new MaterialPropertyBlock();view.enabled=false;}
        public void Play(){elapsed=0;playing=true;view.enabled=true;}
        public void Stop(){elapsed=0;playing=false;if(view!=null)view.enabled=false;}
        private void OnDisable(){Stop();}
        private void LateUpdate(){if(!playing)return;elapsed+=Time.unscaledDeltaTime;int frame=(int)(elapsed*framesPerSecond);if(frames==null||frame>=frames.Length){playing=false;view.enabled=false;return;}if(Camera.main!=null)transform.rotation=Camera.main.transform.rotation;block.SetTexture("_BaseMap",frames[frame]);view.SetPropertyBlock(block);}
    }
}
