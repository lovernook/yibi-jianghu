using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Yibi.Presentation;

namespace Yibi.Editor
{
    public static class CharacterRigBuilder
    {
        public const string Folder="Assets/_Game/Art/Selected/Rigged";
        public static readonly string[] Models={"侠客_劲装","行者_青衫","村中长者","侠女_素衣"};
        public static readonly string[] Actions={"Idle","Walk","Run","Attack","Cast","Hit","Victory","Defeat"};
        static readonly int[] Parents={-1,0,1,2,3,2,5,6,2,8,9,0,11,12,0,14,15,0,0,0,0};
        static readonly string[] Names={"Hips","Spine","Chest","Neck","Head","LeftUpperArm","LeftForearm","LeftHand","RightUpperArm","RightForearm","RightHand","LeftUpperLeg","LeftLowerLeg","LeftFoot","RightUpperLeg","RightLowerLeg","RightFoot","RobeFront","RobeBack","RobeLeft","RobeRight"};
        static readonly Vector3[] Positions={new Vector3(0,1.25f,.07f),new Vector3(0,1.48f,.07f),new Vector3(0,1.77f,.07f),new Vector3(0,2.04f,.06f),new Vector3(0,2.18f,.06f),new Vector3(.27f,1.92f,.07f),new Vector3(.44f,1.55f,.08f),new Vector3(.53f,1.23f,.13f),new Vector3(-.27f,1.92f,.07f),new Vector3(-.44f,1.55f,.08f),new Vector3(-.53f,1.23f,.13f),new Vector3(.17f,1.23f,.07f),new Vector3(.21f,.68f,.07f),new Vector3(.22f,.17f,.07f),new Vector3(-.17f,1.23f,.07f),new Vector3(-.21f,.68f,.07f),new Vector3(-.22f,.17f,.07f),new Vector3(0,1.15f,.23f),new Vector3(0,1.15f,-.14f),new Vector3(.32f,1.15f,.06f),new Vector3(-.32f,1.15f,.06f)};
        [MenuItem("一笔江湖/角色/生成骨骼与动作资产")]
        public static void BuildAll()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("先退出 Play");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            foreach(var name in Models){
                var path=Folder+"/"+name+"_骨骼.asset";var definition=AssetDatabase.LoadAssetAtPath<RigDefinition>(path);
                if(definition==null){definition=ScriptableObject.CreateInstance<RigDefinition>();definition.sourceMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Game/Art/Selected/Characters/"+name+".asset");definition.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Selected/Characters/"+name+".mat");definition.joints=Names.Select((n,i)=>new RigJoint{name=n,parent=Parents[i],position=Positions[i]}).ToArray();if(name=="侠女_素衣"){definition.torsoCenterZ=.3f;foreach(var j in definition.joints)j.position+=new Vector3(-.035f,0,.23f);}if(name=="村中长者"){definition.armThreshold=.36f;foreach(var j in definition.joints)if(j.name.Contains("Arm")||j.name.Contains("Hand"))j.position=new Vector3(j.position.x*1.16f,j.position.y,j.position.z);}AssetDatabase.CreateAsset(definition,path);}
                Build(definition,name);
            }
            AssetDatabase.SaveAssets();
        }
        public static void Build(RigDefinition definition,string name)
        {
            if(definition.sourceMesh==null||definition.joints==null||definition.joints.Length!=21)throw new InvalidOperationException("骨骼配置必须包含 21 个关节和源网格");
            var root=new GameObject(name+"_动态角色");var rig=new GameObject("Rig").transform;rig.SetParent(root.transform,false);
            var joints=new Transform[definition.joints.Length];
            for(int i=0;i<joints.Length;i++){var item=definition.joints[i];if(item.parent>=i)throw new InvalidOperationException("父骨骼必须先于子骨骼");joints[i]=new GameObject(item.name).transform;joints[i].SetParent(item.parent<0?rig:joints[item.parent],false);joints[i].position=item.position;}
            var mesh=UnityEngine.Object.Instantiate(definition.sourceMesh);mesh.name=name+"_蒙皮";
            mesh.bindposes=joints.Select(j=>j.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();
            mesh.boneWeights=mesh.vertices.Select(v=>Weights(v,definition)).ToArray();
            string meshPath=Folder+"/"+name+"_蒙皮.asset";var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(savedMesh==null){AssetDatabase.CreateAsset(mesh,meshPath);savedMesh=mesh;}else{EditorUtility.CopySerialized(mesh,savedMesh);UnityEngine.Object.DestroyImmediate(mesh);}
            var skin=new GameObject("Skin",typeof(SkinnedMeshRenderer)).GetComponent<SkinnedMeshRenderer>();skin.transform.SetParent(root.transform,false);skin.sharedMesh=savedMesh;skin.sharedMaterial=definition.material;skin.bones=joints;skin.rootBone=joints[0];skin.localBounds=new Bounds(new Vector3(0,1.3f,0),new Vector3(4,4,4));skin.quality=SkinQuality.Bone4;skin.updateWhenOffscreen=false;
            var clips=new AnimationClip[Actions.Length];for(int i=0;i<Actions.Length;i++)clips[i]=Clip(Actions[i],joints,root.transform,name,definition.actionScale);
            string controllerPath=Folder+"/"+name+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null){
                controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
                var machine=controller.layers[0].stateMachine;var state=machine.AddState("Locomotion",new Vector3(250,70,0));machine.defaultState=state;
                var tree=new BlendTree{name="移动速度",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(clips[0],0);tree.AddChild(clips[1],3.5f);tree.AddChild(clips[2],6);state.motion=tree;
                for(int i=3;i<Actions.Length;i++){var action=machine.AddState(Actions[i],new Vector3(250+(i%2)*260,160+(i-3)*75,0));action.motion=clips[i];action.writeDefaultValues=true;}
            }
            var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.updateMode=AnimatorUpdateMode.UnscaledTime;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;root.AddComponent<CharacterMotion>().animator=animator;
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
        }
        static BoneWeight Weights(Vector3 v,RigDefinition d)
        {
            int[] candidates;bool left=v.x>=0;
            if(v.y>2.03f)candidates=new[]{3,4,2};
            else if(v.y>1.03f&&v.y<2.04f&&Mathf.Abs(v.x)>d.armThreshold)candidates=left?new[]{5,6,7,2}:new[]{8,9,10,2};
            else if(v.y<1.24f){
                bool cloth=v.y>.28f&&(Mathf.Abs(v.x)>.34f||v.z-d.torsoCenterZ<-.16f||v.z-d.torsoCenterZ>.23f);
                candidates=cloth?new[]{17,18,19,20,0}:left?new[]{11,12,13,0}:new[]{14,15,16,0};
            }else candidates=new[]{0,1,2,3};
            var entries=new List<KeyValuePair<int,float>>();
            foreach(int i in candidates){var start=d.joints[i].position;Vector3 end;
                if(i==0)end=d.joints[1].position;else if(i==1)end=d.joints[2].position;else if(i==2)end=d.joints[3].position;else if(i==3)end=d.joints[4].position;else if(i==4)end=start+Vector3.up*.23f;
                else if(i==5||i==6||i==8||i==9||i==11||i==12||i==14||i==15)end=d.joints[i+1].position;
                else if(i==13||i==16)end=start+new Vector3(0,-.08f,.21f);else end=start+Vector3.down*(i>=17?.72f:.14f);
                var delta=end-start;float t=Mathf.Clamp01(Vector3.Dot(v-start,delta)/Mathf.Max(delta.sqrMagnitude,.0001f));float distance=(v-start-delta*t).sqrMagnitude;
                float weight=1/Mathf.Pow(distance+.014f,2.5f);if((i==0&&v.y<.85f)||(i==2&&Mathf.Abs(v.x)>.42f))weight*=.05f;
                entries.Add(new KeyValuePair<int,float>(i,weight));
            }
            entries=entries.OrderByDescending(p=>p.Value).Take(4).ToList();float sum=entries.Sum(p=>p.Value);var result=new BoneWeight();
            result.boneIndex0=entries[0].Key;result.weight0=entries[0].Value/sum;
            if(entries.Count>1){result.boneIndex1=entries[1].Key;result.weight1=entries[1].Value/sum;}if(entries.Count>2){result.boneIndex2=entries[2].Key;result.weight2=entries[2].Value/sum;}if(entries.Count>3){result.boneIndex3=entries[3].Key;result.weight3=entries[3].Value/sum;}return result;
        }
        static AnimationClip Clip(string action,Transform[] joints,Transform root,string model,float amplitude)
        {
            float duration=action=="Idle"?2.4f:action=="Walk"?1:action=="Run"?.68f:action=="Attack"?.86f:action=="Hit"?.42f:1.15f;
            string path=Folder+"/"+model+"_"+action+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip{name=action,frameRate=30};AssetDatabase.CreateAsset(clip,path);}else clip.ClearCurves();
            int samples=30;
            for(int i=0;i<joints.Length;i++){
                string binding=AnimationUtility.CalculateTransformPath(joints[i],root);var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int frame=0;frame<=samples;frame++){float t=frame/(float)samples;var q=Quaternion.Euler(Pose(action,i,t)*(action=="Defeat"?1:amplitude));float time=t*duration;curves[0].AddKey(time,q.x);curves[1].AddKey(time,q.y);curves[2].AddKey(time,q.z);curves[3].AddKey(time,q.w);}
                for(int channel=0;channel<4;channel++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(binding,typeof(Transform),"m_LocalRotation."+"xyzw"[channel]),curves[channel]);
            }
            var y=new AnimationCurve();for(int frame=0;frame<=samples;frame++){float t=frame/(float)samples;float offset=0;if(action=="Walk"||action=="Run")offset=Mathf.Abs(Mathf.Sin(t*Mathf.PI*2))*(action=="Run"?.07f:.035f);else if(action=="Idle")offset=Mathf.Sin(t*Mathf.PI*2)*.012f;else if(action=="Defeat")offset=-.45f*Mathf.SmoothStep(0,1,t);y.AddKey(t*duration,joints[0].localPosition.y+offset);}
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(joints[0],root),typeof(Transform),"m_LocalPosition.y"),y);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=action=="Idle"||action=="Walk"||action=="Run";settings.loopBlend=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);return clip;
        }
        static Vector3 Pose(string action,int i,float t)
        {
            float wave=Mathf.Sin(t*Mathf.PI*2);float beat=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.7f));float strike=Mathf.Sin(Mathf.PI*Mathf.Clamp01((t-.12f)/.58f));var e=Vector3.zero;
            if(action=="Idle"){if(i==1)e.x=wave*.8f;if(i==4)e.y=wave*1.3f;if(i==5)e.z=-3+wave;if(i==8)e.z=3-wave;if(i>=17)e.x=wave*1.2f;}
            if(action=="Walk"||action=="Run"){
                float amplitude=action=="Run"?32:20;if(i==11)e.x=wave*amplitude;if(i==14)e.x=-wave*amplitude;if(i==12)e.x=Mathf.Max(0,-wave)*-38;if(i==15)e.x=Mathf.Max(0,wave)*-38;
                if(i==5)e.x=-wave*amplitude*.65f;if(i==8)e.x=wave*amplitude*.65f;if(i==6||i==9)e.x=-12;if(i==1){e.x=action=="Run"?8:3;e.y=wave*2;}if(i>=17)e.x=wave*5;
            }
            if(action=="Attack"){
                if(i==1)e.y=-18*beat;if(i==2)e.y=28*strike;if(i==8)e.x=-100*strike;if(i==9)e.x=-24*beat;if(i==10)e.z=12*strike;if(i==5){e.x=-30*beat;e.z=-12*beat;}if(i==6)e.x=-45*beat;if(i==14)e.x=10*beat;
            }
            if(action=="Cast"){if(i==5){e.x=-65*beat;e.z=-15*beat;}if(i==8){e.x=-65*beat;e.z=15*beat;}if(i==6||i==9)e.x=-35*beat;if(i==1)e.x=-5*beat;if(i==4)e.x=8*beat;if(i>=17)e.x=wave*6;}
            if(action=="Hit"){if(i==1)e.x=-14*beat;if(i==4)e.x=-12*beat;if(i==5)e.z=8*beat;if(i==8)e.z=-8*beat;}
            if(action=="Victory"){float hold=Mathf.SmoothStep(0,1,Mathf.Min(t*2,1));if(i==5){e.x=-65*hold;e.z=-18*hold;}if(i==8){e.x=-65*hold;e.z=18*hold;}if(i==6||i==9)e.x=-42*hold;if(i==4)e.x=12*hold;}
            // A low kneel keeps the pelvis upright: rotating it backwards also rotates
            // both legs and robe, which previously lifted the feet above the torso.
            if(action=="Defeat"){float hold=Mathf.SmoothStep(0,1,t);if(i==1)e.x=35*hold;if(i==11||i==14)e.x=-55*hold;if(i==12||i==15)e.x=105*hold;if(i==4)e.x=20*hold;if(i>=17)e.x=-55*hold;}
            return e;
        }
        [MenuItem("一笔江湖/角色/打开动作工作台")]
        public static void Workshop()
        {
            string path="Assets/_Game/Scenes/AnimationWorkshop.unity";if(File.Exists(path)){EditorSceneManager.OpenScene(path);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);var panel=new GameObject("动作预览",typeof(AnimationWorkshop)).GetComponent<AnimationWorkshop>();panel.actors=new CharacterMotion[4];
            for(int i=0;i<4;i++){var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+Models[i]+".prefab"));actor.transform.position=new Vector3((i-1.5f)*3,0,0);actor.transform.rotation=Quaternion.Euler(0,180,0);panel.actors[i]=actor.GetComponent<CharacterMotion>();}
            ArenaSetup.Shape("预览地面",PrimitiveType.Cube,null,new Vector3(0,-.15f,0),new Vector3(18,.3f,8),AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Selected/资源_擂台地砖.mat"));
            Camera.main.transform.position=new Vector3(0,3,-10);Camera.main.transform.LookAt(new Vector3(0,1.1f,0));Camera.main.fieldOfView=45;RenderSettings.ambientLight=Color.gray;
            var canvas=new GameObject("动作工具栏",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;canvas.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1280,720);
            var font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);panel.state=ArenaSetup.Label("动作状态",canvas.transform,"选择动作预览 · 四位角色拥有独立骨骼和蒙皮",new Vector2(0,280),new Vector2(1150,80),24);panel.state.alignment=TextAnchor.MiddleCenter;
            for(int i=0;i<8;i++){var b=ArenaSetup.Button(Actions[i],canvas.transform,new[]{"待机","行走","奔跑","出掌","运功","受击","胜利","倒地"}[i],new Vector2(-525+i*150,-280),new Vector2(140,55));UnityEventTools.AddIntPersistentListener(b.onClick,panel.Preview,i);b.image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/UI/青玉按钮.asset");b.image.type=Image.Type.Sliced;}
            foreach(var text in canvas.GetComponentsInChildren<Text>())text.font=font;new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));EditorSceneManager.SaveScene(scene,path);
        }
    }
    [CustomEditor(typeof(RigDefinition))]
    public sealed class RigDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();EditorGUILayout.HelpBox("关节位置是角色根节点坐标。保存配置后重新生成；原始静态网格不修改。生成会重写此角色的蒙皮、动画和 Prefab，手调 .anim 前先复制。",MessageType.Info);if(GUILayout.Button("按当前关节配置重新绑定")){var d=(RigDefinition)target;CharacterRigBuilder.Build(d,d.name.Replace("_骨骼",""));AssetDatabase.SaveAssets();}}
    }
}
