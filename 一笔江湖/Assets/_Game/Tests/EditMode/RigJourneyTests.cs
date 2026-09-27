using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Yibi.Rules;
using Yibi.Battle;
using Yibi.Presentation;

public class RigJourneyTests
{
    const string Folder="Assets/_Game/Art/Selected/Rigged/";
    [TestCase("侠客_劲装")][TestCase("行者_青衫")][TestCase("村中长者")][TestCase("侠女_素衣")]
    public void SavedSkinHasNormalizedWeightsAndCorrectBindPose(string model)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+model+".prefab");Assert.IsNotNull(prefab);
        var actor=UnityEngine.Object.Instantiate(prefab);var baked=new Mesh();
        try{
            var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();Assert.AreEqual(21,skin.bones.Length);Assert.AreEqual(skin.bones.Length,skin.sharedMesh.bindposes.Length);Assert.AreEqual(skin.sharedMesh.vertexCount,skin.sharedMesh.boneWeights.Length);
            foreach(var w in skin.sharedMesh.boneWeights){Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.00001));Assert.That(w.boneIndex0,Is.InRange(0,20));Assert.GreaterOrEqual(w.weight0,w.weight1);}
            skin.BakeMesh(baked);var source=skin.sharedMesh.vertices;var actual=baked.vertices;float max=0;for(int i=0;i<source.Length;i++)max=Mathf.Max(max,Vector3.Distance(source[i],actual[i]));Assert.Less(max,.001f,"Binding must not move the original mesh in its rest pose");
        }finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(actor);}
    }
    [TestCase("侠客_劲装")][TestCase("村中长者")]
    public void AttackMovesTheHandAndSkinnedVerticesWithoutMovingActorRoot(string model)
    {
        var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+model+".prefab"));var baked=new Mesh();
        try{
            actor.GetComponent<Animator>().enabled=false;var hand=actor.transform.Find("Rig/Hips/Spine/Chest/RightUpperArm/RightForearm/RightHand");var before=hand.position;var root=actor.transform.position;
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+model+"_Attack.anim").SampleAnimation(actor,.35f);
            Assert.Greater(Vector3.Distance(before,hand.position),.35f);Assert.AreEqual(root,actor.transform.position);
            var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(baked);var source=skin.sharedMesh.vertices;var moved=baked.vertices;Assert.Greater(Enumerable.Range(0,moved.Length).Count(i=>Vector3.Distance(moved[i],source[i])>.1f),100);Assert.Less(baked.bounds.size.magnitude,5,"No exploding skin");
        }finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(actor);}
    }
    [TestCase("Idle")][TestCase("Walk")][TestCase("Run")]
    public void LoopedClipsReturnToTheSameBonePose(string action)
    {
        var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"侠客_劲装.prefab"));
        try{actor.GetComponent<Animator>().enabled=false;var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"侠客_劲装_"+action+".anim");clip.SampleAnimation(actor,0);var bones=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones;var positions=bones.Select(x=>x.position).ToArray();clip.SampleAnimation(actor,clip.length);for(int i=0;i<bones.Length;i++)Assert.Less(Vector3.Distance(positions[i],bones[i].position),.001f);}
        finally{UnityEngine.Object.DestroyImmediate(actor);}
    }
    [Test] public void FinalChallengeNeedsBothExplorationAndFirstDuel()
    {
        var done=new System.Collections.Generic.List<string>();Assert.AreEqual(0,JourneyRules.Stage(done));done.Add("first-win-0");Assert.IsFalse(JourneyRules.CanChallengeFinal(done));done.Add("valley-trial");Assert.IsTrue(JourneyRules.CanChallengeFinal(done));Assert.AreEqual(0,JourneyRules.Stage(done),"Out-of-order completion must not skip practice");done.Add("practice-pass");Assert.AreEqual(3,JourneyRules.Stage(done));done.Add("first-win-1");Assert.AreEqual(4,JourneyRules.Stage(done));
    }
    [Test] public void ReopeningRewardsCannotDuplicateTickets()
    {
        var p=new PlayerProfile();using(ProfileStore.UseTransientProfile(p)){Assert.IsTrue(ProfileStore.Reward("cache-test",1));Assert.IsFalse(ProfileStore.Reward("cache-test",1));Assert.AreEqual(3,p.tickets);Assert.AreEqual(1,p.achievements.Count);}
    }
    [Test] public void DrawKeepsLoadoutOwnedAndNeverDuplicatesUntilCollectionIsComplete()
    {
        var p=new PlayerProfile{tickets=4};using(ProfileStore.UseTransientProfile(p)){for(int i=0;i<3;i++)ProfileStore.Draw(7);Assert.AreEqual(6,p.unlocked.Count);Assert.AreEqual(6,p.unlocked.Distinct().Count());ProfileStore.Draw(7);Assert.AreEqual(1,p.fragments);ProfileStore.Validate(p);}
    }
}
