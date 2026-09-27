#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Yibi.Presentation;

public sealed class StageCMotionPlayTests
{
    private GameObject actor;
    private CharacterMotion motion;
    private CharacterMotionProfile profile;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Selected/Rigged/侠客_劲装.prefab");
        Assert.IsNotNull(prefab);
        actor = Object.Instantiate(prefab); actor.transform.position = Vector3.one * 1000;
        motion = actor.GetComponent<CharacterMotion>();
        Assert.IsNotNull(motion.profile);
        profile = Object.Instantiate(motion.profile); motion.profile = profile;
        yield return null;
        motion.ResetPose(); yield return null;
        Assert.IsEmpty(motion.ValidateConfiguration());
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (actor != null) Object.Destroy(actor);
        if (profile != null) Object.Destroy(profile);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CustomIdUsesSavedAttackStateAndConfiguredReturnTime()
    {
        profile.actions[0].id = "test.heavy";
        profile.actions[0].durationSeconds = .45f;
        profile.actions[0].transitionSeconds = .01f;
        Assert.IsTrue(motion.TryPlayAction("test.heavy"));
        yield return new WaitForSecondsRealtime(.08f);
        Assert.AreEqual("test.heavy", motion.CurrentAction);
        Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"));
        yield return new WaitForSecondsRealtime(.65f);
        Assert.AreEqual("Locomotion", motion.CurrentAction);
        Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator TerminalPoseRejectsNormalActionsUntilExplicitReset()
    {
        Assert.IsTrue(motion.TryPlayAction("Defeat"));
        Assert.IsFalse(motion.TryPlayAction("Attack"));
        yield return null;
        Assert.AreEqual("Defeat", motion.CurrentAction);
        motion.ResetPose();
        Assert.AreEqual("Locomotion", motion.CurrentAction);
        Assert.IsTrue(motion.TryPlayAction("Attack"));
        yield return new WaitForSecondsRealtime(.12f);
        Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator MissingControllerStateReturnsSafelyToLocomotion()
    {
        Assert.IsTrue(motion.TryPlayAction("Attack"));
        yield return new WaitForSecondsRealtime(.12f);
        profile.actions[0].stateName = "State_That_Does_Not_Exist";
        Assert.IsNotEmpty(motion.ValidateConfiguration());
        Assert.IsFalse(motion.TryPlayAction("Attack"));
        yield return new WaitForSecondsRealtime(.25f);
        Assert.AreEqual("Locomotion", motion.CurrentAction);
        Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
        Assert.IsFalse(motion.TryPlayAction("unknown-action"));
        LogAssert.NoUnexpectedReceived();
    }
}
#endif
