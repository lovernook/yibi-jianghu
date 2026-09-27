using UnityEngine;
using Yibi.Presentation;

namespace Yibi.World
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ValleyExplorer:MonoBehaviour
    {
        public float speed=3.5f;
        public float sprintSpeed=6;
        public Vector3 respawnPoint=new Vector3(0,1,-14);
        public CharacterMotion motion;
        public Transform visual;
        public Camera followCamera;
        public Vector3 cameraOffset=new Vector3(0,15,-14);
        public bool inputLocked;
        public bool IsInputBlocked => inputLocked || Yibi.App.GameNavigation.IsInputBlocked;
        private CharacterController controller;
        private float fall;
        private float zoom=1;
        private readonly RaycastHit[] cameraHits=new RaycastHit[32];
        private void Awake(){controller=GetComponent<CharacterController>();if(motion==null)motion=GetComponentInChildren<CharacterMotion>();}
        private void Update()
        {
            Vector3 direction=IsInputBlocked?Vector3.zero:Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical")),1);
            fall=controller.isGrounded?-2:Mathf.Max(-30,fall-25*Time.deltaTime);
            float moveSpeed=Input.GetKey(KeyCode.LeftShift)?sprintSpeed:speed;
            Vector3 before=transform.position;controller.Move((direction*moveSpeed+Vector3.up*fall)*Time.deltaTime);
            var displacement=transform.position-before;displacement.y=0;
            if(motion!=null)motion.SetSpeed(displacement.magnitude/Mathf.Max(Time.deltaTime,.0001f));
            if(direction.sqrMagnitude>.01f)visual.rotation=Quaternion.Slerp(visual.rotation,Quaternion.LookRotation(direction),Time.deltaTime*12);
            if(transform.position.y<-8){controller.enabled=false;transform.position=respawnPoint;controller.enabled=true;fall=0;}
        }
        private void LateUpdate()
        {
            if(followCamera==null)return;
            if(!IsInputBlocked)zoom=Mathf.Clamp(zoom-Input.mouseScrollDelta.y*.06f,.72f,1.15f);
            Vector3 target=transform.position+Vector3.up*1.6f,offset=cameraOffset*zoom;
            float distance=offset.magnitude;var direction=offset.normalized;
            int count=Physics.SphereCastNonAlloc(target,.32f,direction,cameraHits,distance,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!cameraHits[i].transform.IsChildOf(transform))distance=Mathf.Min(distance,Mathf.Max(2,cameraHits[i].distance-.35f));
            var desired=target+direction*distance;
            // Pull in immediately at an obstacle; ease back out to avoid looking through walls.
            followCamera.transform.position=distance<offset.magnitude-.01f?desired:Vector3.Lerp(followCamera.transform.position,desired,1-Mathf.Exp(-8*Time.deltaTime));followCamera.transform.LookAt(target);
        }
    }
}
