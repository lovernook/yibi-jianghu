#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yibi.App;
using Yibi.Battle;
using Yibi.Networking;
using Yibi.Presentation;
using Yibi.UI;
using Yibi.World;
using Object = UnityEngine.Object;

public sealed class StageDPresentationPlayTests
{
    const string NativeRoot = "Assets/_Game/Art/Selected/StageD/Characters/";
    private IDisposable profile;
    private int previousStrategy;

    [UnitySetUp]
    public IEnumerator Isolate()
    {
        foreach (var session in Object.FindObjectsOfType<NetSession>(true)) Object.Destroy(session.gameObject);
        if (GameRoot.Instance != null && GameRoot.Instance.HasError) GameRoot.Instance.DismissError();
        previousStrategy = ProfileStore.NpcStrategy; ProfileStore.NpcStrategy = 0;
        profile = ProfileStore.UseTransientProfile(new PlayerProfile());
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Restore()
    {
        try {
            foreach (var session in Object.FindObjectsOfType<NetSession>(true)) Object.Destroy(session.gameObject);
            yield return SceneManager.LoadSceneAsync("Bootstrap"); yield return null;
        }
        finally { profile?.Dispose(); ProfileStore.NpcStrategy = previousStrategy; }
    }

    [UnityTest]
    public IEnumerator SavedValleyNativePlayerAndPurchasedJournalSurviveOrdinaryUiRefresh()
    {
        yield return Load("Valley");
        var valley = SceneComponents<ValleyPresenter>().Single();
        Assert.IsNotNull(valley.explorer.visual);
        var motion = AssertNativeActor(valley.explorer.visual);
        Assert.AreSame(motion, valley.explorer.motion, "Movement must drive the saved native actor.");
        Assert.IsTrue(motion.transform.IsChildOf(valley.explorer.visual));
        Assert.IsTrue(valley.explorer.visual.IsChildOf(valley.explorer.transform));
        AssertNoVisibleLegacySkin();
        var teacher = SceneComponents<Transform>().Single(t => t.name == "听松先生");
        var teacherBounds = MeasureBakedWorldBounds(teacher.gameObject);
        Assert.That(teacherBounds.min.y, Is.EqualTo(1.2f).Within(.12f), "The native teacher's visible feet must remain on the saved platform.");

        // Exercise the saved button callback, rather than constructing a replacement panel in the test.
        var journal = valley.journal;
        var open = valley.GetComponentsInChildren<Button>(true).FirstOrDefault(b => HasCallback(b, journal, "Toggle"));
        Assert.IsNotNull(open, "The saved task entry must retain its persistent action.");
        open.onClick.Invoke(); yield return null;
        Assert.IsTrue(journal.IsOpen); Assert.IsTrue(valley.explorer.IsInputBlocked);
        var journalFocus = journal.panel.GetComponent<ModalFocusScope>();
        Assert.IsNotNull(journalFocus); Assert.IsNotNull(journalFocus.defaultSelection);
        var closeLabel = journalFocus.defaultSelection.GetComponentInChildren<Text>();
        Assert.IsNotNull(closeLabel);
        Assert.Greater(closeLabel.color.grayscale, .65f, "The journal's dark close button needs a readable light label.");
        var skins = CapturePurchasedSkin();
        foreach (var row in journal.content.GetComponentsInChildren<QuestRowView>()) {
            Assert.IsNotNull(row.GetComponent<UISkinBinding>(), "Cloned rows must retain their saved skin binding.");
            AssertLiveText(row.title);
        }
        for (int i = 0; i < 3; i++) { journal.Refresh(); valley.journey.Refresh(); }
        journal.Close(); valley.ToggleInventory();
        valley.journey.SelectTechnique(1);
        valley.journey.equipSelected[0].onClick.Invoke();
        valley.RefreshInventory();
        Assert.AreEqual("lieshi", ProfileStore.Current.loadout[0], "Saved equipment button remains functional after skinning.");
        valley.ToggleInventory();
        valley.journey.OpenDialogue(2); Assert.IsFalse(valley.journey.accept.interactable); valley.journey.CloseDialogue();
        journal.Open(); yield return null;
        AssertSkinUnchanged(skins);
        journal.Close(); yield return null;
        Assert.IsFalse(valley.explorer.IsInputBlocked);
    }

    [UnityTest]
    public IEnumerator BothArenasUseActualNativeBoneAnimationHoldDefeatAndKeepBattleEffects()
    {
        foreach (string scene in new[] {"Arena_Stone", "Arena_Bamboo"}) {
            yield return Load(scene);
            var battle = SceneComponents<BattlePresenter>().Single();
            AssertNoVisibleLegacySkin();
            // Isolate the animation contract from the unrelated 25-second turn deadline.
            battle.enabled = false;
            var roots = new[] {battle.leftActor, battle.rightActor};
            var motions = roots.Select(AssertNativeActor).ToArray();
            for (int seat = 0; seat < roots.Length; seat++) {
                var motion = motions[seat];
                Assert.IsEmpty(motion.ValidateConfiguration(), scene + " seat " + seat);
                yield return ObserveAttackBones(motion);
                var origin = roots[seat].localPosition;
                Assert.IsTrue(motion.TryPlayAction("Defeat"));
                CharacterMotionAction death; Assert.IsTrue(motion.profile.TryGet("Defeat", out death));
                Assert.IsTrue(death.holdUntilReset);
                yield return new WaitForSecondsRealtime(death.durationSeconds + death.transitionSeconds + .15f);
                Assert.AreEqual("Defeat", motion.CurrentAction);
                Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName(death.stateName));
                var deathClips = motion.animator.GetCurrentAnimatorClipInfo(0);
                Assert.IsTrue(deathClips.Any(c => c.clip.name == "Die" && !c.clip.isLooping), "Defeat must use and hold the source Die clip.");
                var held = CaptureBones(motion.transform);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.AreEqual(0, ChangedBones(held), "The non-looping death pose must stay at its final frame.");
                Assert.IsFalse(motion.TryPlayAction("Attack"));
                Assert.Less(Vector3.Distance(origin, roots[seat].localPosition), .001f, "Native root motion cannot move the battle actor container.");
                motion.ResetPose(); yield return null;
                Assert.AreEqual("Locomotion", motion.CurrentAction);
                Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName(motion.profile.locomotionState));
            }

            Assert.AreEqual(2, battle.feedback.attackEffects.Length);
            Assert.AreEqual(2, battle.feedback.supportEffects.Length);
            for (int seat = 0; seat < 2; seat++) {
                AssertEffect(battle.feedback.attackEffects[seat], roots[seat]);
                AssertEffect(battle.feedback.supportEffects[seat], roots[seat]);
                Assert.AreNotSame(battle.feedback.attackEffects[seat], battle.feedback.supportEffects[seat]);
            }
            var leftHome = battle.leftActor.localPosition; var rightHome = battle.rightActor.localPosition;
            battle.enabled = true;
            battle.Basic();
            Assert.AreEqual(1, battle.State.revision);
            Assert.AreEqual("Attack", motions[0].CurrentAction, "BattleFeedback must target the replacement native CharacterMotion.");
            Assert.IsTrue(battle.feedback.attackEffects[0].GetComponent<MeshRenderer>().enabled);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.Greater(Vector3.Distance(leftHome, battle.leftActor.localPosition), .05f);
            battle.Restart(); yield return null;
            Assert.AreEqual(leftHome, battle.leftActor.localPosition); Assert.AreEqual(rightHome, battle.rightActor.localPosition);
            Assert.IsTrue(motions.All(m => m.CurrentAction == "Locomotion"));
            foreach (var effect in battle.feedback.attackEffects.Concat(battle.feedback.supportEffects))
                Assert.IsFalse(effect.GetComponent<MeshRenderer>().enabled, "Restart must stop saved effects.");
        }
    }

    [UnityTest]
    public IEnumerator SavedProductionPagesRetainPurchasedSkinsFontsAndPersistentButtons()
    {
        foreach (string scene in new[] {"MainMenu", "Valley", "Arena_Stone", "Arena_Bamboo", "GestureLab", "Lobby", "AnimationWorkshop"}) {
            yield return Load(scene);
            var skins = CapturePurchasedSkin();
            Assert.Greater(skins.Count, 0, scene + " must save actual skin bindings, not just load a skin asset.");
            int wired = 0;
            foreach (var binding in SceneComponents<UISkinBinding>()) {
                foreach (var slot in binding.images) {
                    if (slot == null || slot.target == null) continue;
                    var button = slot.target.GetComponent<Button>();
                    if (button == null || button.GetComponentInParent<QuestRowView>() != null) continue;
                    Assert.Greater(button.onClick.GetPersistentEventCount(), 0, scene + "/" + button.name);
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++) {
                        Assert.IsNotNull(button.onClick.GetPersistentTarget(i), scene + "/" + button.name);
                        Assert.IsNotEmpty(button.onClick.GetPersistentMethodName(i));
                    }
                    wired++;
                }
            }
            Assert.Greater(wired, 0, scene);
            foreach (var text in SceneComponents<Text>()) if (text.font != null) AssertLiveText(text);
            yield return null;
            AssertSkinUnchanged(skins);
        }
    }

    [UnityTest]
    public IEnumerator WorkshopFourNativeCombatRolesActuallyAnimateWithoutFixedBoneNames()
    {
        yield return Load("AnimationWorkshop");
        var workshop = SceneComponents<AnimationWorkshop>().Single();
        Assert.AreEqual(4, workshop.actors.Length);
        AssertNoVisibleLegacySkin();
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actor in workshop.actors) {
            Assert.IsNotNull(actor);
            Assert.AreSame(actor, AssertNativeActor(actor.transform));
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            string path = AssetDatabase.GetAssetPath(skin.sharedMesh);
            roles.Add(path.Substring(NativeRoot.Length).Split('/')[0]);
            yield return ObserveAttackBones(actor);
            actor.ResetPose();
        }
        CollectionAssert.AreEquivalent(new[] {"PlayerSwordsman", "SwordMaster", "Bandit", "TrainingDummy"}, roles);
    }

    [UnityTest]
    public IEnumerator NativePrefabWorldSizesAreCorrectAndDummyResetDoesNotRetainDeathFragments()
    {
        yield return Load("Bootstrap");
        foreach (string id in new[] {"PlayerSwordsman", "SwordMaster", "Bandit", "TrainingDummy", "Physician", "Innkeeper", "Swordswoman"}) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NativeRoot + id + "/" + id + ".prefab");
            Assert.IsNotNull(prefab, id);
            var actor = Object.Instantiate(prefab);
            try {
                actor.transform.position = new Vector3(1000, 1000, 1000);
                var motion = AssertNativeActor(actor.transform);
                motion.ResetPose();
                yield return new WaitForSecondsRealtime(.1f);
                Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName(motion.profile.locomotionState));
                var initial = MeasureBakedWorldBounds(actor);
                float targetHeight = id == "TrainingDummy" ? 2.2f : 2.4f;
                Assert.That(initial.size.y, Is.EqualTo(targetHeight).Within(.2f), id + " must use the intended world height, not a twice-scaled editor estimate.");
                if (id != "TrainingDummy") continue;

                CharacterMotionAction death; Assert.IsTrue(motion.profile.TryGet("Defeat", out death));
                Assert.IsTrue(motion.TryPlayAction("Defeat"));
                yield return new WaitForSecondsRealtime(death.durationSeconds + death.transitionSeconds + .15f);
                Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName(death.stateName));
                motion.ResetPose();
                yield return new WaitForSecondsRealtime(.1f);
                var reset = MeasureBakedWorldBounds(actor);
                Assert.That(reset.size.y, Is.EqualTo(targetHeight).Within(.2f));
                Assert.Less(Vector3.Distance(initial.size, reset.size), .2f, "Reset must reassemble the dummy's full size, including width and depth.");
                Assert.Less(Vector3.Distance(initial.center, reset.center), .15f, "Death fragments must not remain displaced after returning to Stand.");
            }
            finally { Object.Destroy(actor); }
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator NativeLiveStandAttackAndDeathFitSavedCullingBounds()
    {
        yield return Load("Bootstrap");
        var scratch = new Mesh();
        var vertices = new List<Vector3>();
        try {
            foreach (string id in new[] {"PlayerSwordsman", "SwordMaster", "Bandit", "TrainingDummy", "Physician", "Innkeeper", "Swordswoman"}) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NativeRoot + id + "/" + id + ".prefab");
                Assert.IsNotNull(prefab, id);
                var actor = Object.Instantiate(prefab);
                try {
                    actor.transform.SetPositionAndRotation(new Vector3(1000, 1000, 1000), Quaternion.Euler(0, 35, 0));
                    var motion = AssertNativeActor(actor.transform);
                    motion.ResetPose();
                    yield return new WaitForSecondsRealtime(.1f);
                    AssertBakedInsideCulling(actor, scratch, vertices, id + "/Stand");
                    if (id == "PlayerSwordsman" || id == "SwordMaster" || id == "Bandit" || id == "TrainingDummy") {
                        CharacterMotionAction attack; Assert.IsTrue(motion.profile.TryGet("Attack", out attack));
                        Assert.IsTrue(motion.TryPlayAction("Attack"));
                        yield return new WaitForSecondsRealtime(attack.transitionSeconds + .04f);
                        AssertBakedInsideCulling(actor, scratch, vertices, id + "/Attack start");
                        yield return new WaitForSecondsRealtime(Mathf.Min(.2f, attack.durationSeconds * .25f));
                        AssertBakedInsideCulling(actor, scratch, vertices, id + "/Attack middle");
                        motion.ResetPose(); yield return null;
                        CharacterMotionAction death; Assert.IsTrue(motion.profile.TryGet("Defeat", out death));
                        Assert.IsTrue(motion.TryPlayAction("Defeat"));
                        yield return new WaitForSecondsRealtime(death.transitionSeconds + .04f);
                        AssertBakedInsideCulling(actor, scratch, vertices, id + "/Die start");
                        yield return new WaitForSecondsRealtime(death.durationSeconds + .15f);
                        Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName(death.stateName));
                        AssertBakedInsideCulling(actor, scratch, vertices, id + "/Die final");
                    }
                }
                finally { Object.Destroy(actor); }
                yield return null;
            }
        }
        finally { Object.Destroy(scratch); }
    }

    private static void AssertBakedInsideCulling(GameObject actor, Mesh scratch, List<Vector3> vertices, string context)
    {
        int rendererCount = 0;
        foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if (!skin.enabled || !skin.gameObject.activeInHierarchy) continue;
            rendererCount++;
            skin.BakeMesh(scratch, true);
            vertices.Clear(); scratch.GetVertices(vertices);
            Assert.Greater(vertices.Count, 0, context + "/" + skin.name);
            var culling = skin.bounds;
            var min = culling.min; var max = culling.max;
            Assert.IsFalse(float.IsNaN(culling.size.sqrMagnitude) || float.IsInfinity(culling.size.sqrMagnitude), context);
            Assert.Greater(culling.size.sqrMagnitude, 0, context);
            float outside = 0;
            bool finite = true;
            foreach (var vertex in vertices) {
                var point = skin.transform.TransformPoint(vertex);
                finite &= !float.IsNaN(point.sqrMagnitude) && !float.IsInfinity(point.sqrMagnitude);
                outside = Mathf.Max(outside, Mathf.Max(min.x - point.x, point.x - max.x));
                outside = Mathf.Max(outside, Mathf.Max(min.y - point.y, point.y - max.y));
                outside = Mathf.Max(outside, Mathf.Max(min.z - point.z, point.z - max.z));
            }
            Assert.IsTrue(finite, context + " has a non-finite baked vertex.");
            Assert.LessOrEqual(outside, .005f, context + "/" + skin.name + " visible vertices escape the runtime culling bounds; check rootBone space.");
        }
        Assert.Greater(rendererCount, 0, context);
    }

    private static IEnumerator ObserveAttackBones(CharacterMotion motion)
    {
        motion.ResetPose(); yield return null;
        CharacterMotionAction action; Assert.IsTrue(motion.profile.TryGet("Attack", out action));
        Assert.IsTrue(motion.TryPlayAction("Attack"));
        yield return new WaitForSecondsRealtime(action.transitionSeconds + .02f);
        Assert.IsTrue(motion.animator.GetCurrentAnimatorStateInfo(0).IsName(action.stateName));
        Assert.IsTrue(motion.animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Attack" && AssetDatabase.GetAssetPath(c.clip).StartsWith(NativeRoot, StringComparison.Ordinal)), "Use the selected FBX's original attack, not a generated placeholder.");
        var before = CaptureBones(motion.transform);
        float interval = Mathf.Min(.08f, Mathf.Max(.01f, (action.durationSeconds - action.transitionSeconds - .04f) / 5));
        int changed = 0;
        for (int i = 0; i < 4; i++) { yield return new WaitForSecondsRealtime(interval); changed = Math.Max(changed, ChangedBones(before)); }
        Assert.Greater(changed, 0, "A state label is insufficient: weighted native bones must change during the original attack.");
    }

    private static CharacterMotion AssertNativeActor(Transform root)
    {
        Assert.IsNotNull(root);
        var motions = root.GetComponentsInChildren<CharacterMotion>(true);
        Assert.AreEqual(1, motions.Length, root.name + " must not retain an old motion driver.");
        var motion = motions[0];
        Assert.IsTrue(motion.isActiveAndEnabled);
        Assert.IsNotNull(motion.profile); Assert.IsNotNull(motion.animator);
        Assert.IsFalse(motion.animator.applyRootMotion);
        Assert.IsNotNull(motion.animator.avatar); Assert.IsTrue(motion.animator.avatar.isValid);
        Assert.AreEqual(1, root.GetComponentsInChildren<Animator>(true).Length);
        var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.enabled && s.gameObject.activeInHierarchy).ToArray();
        Assert.Greater(skins.Length, 0, root.name);
        foreach (var skin in skins) {
            Assert.IsNotNull(skin.sharedMesh); Assert.IsNotNull(skin.rootBone);
            StringAssert.StartsWith(NativeRoot, AssetDatabase.GetAssetPath(skin.sharedMesh));
            Assert.Greater(skin.bones.Length, 0); Assert.IsTrue(skin.bones.All(b => b != null));
            Assert.IsTrue(skin.bones.All(b => b.IsChildOf(motion.animator.transform)), "Animator binding root must contain the weighted skeleton.");
            Assert.IsTrue(skin.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported));
        }
        return motion;
    }

    private static Dictionary<Transform, Pose> CaptureBones(Transform actor)
    {
        var bones = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s => s.bones).Where(b => b != null).Distinct();
        return bones.ToDictionary(b => b, b => new Pose(b.localPosition, b.localRotation));
    }

    private static int ChangedBones(Dictionary<Transform, Pose> before)
    {
        return before.Count(pair => Vector3.Distance(pair.Key.localPosition, pair.Value.position) > .0002f || Quaternion.Angle(pair.Key.localRotation, pair.Value.rotation) > .1f);
    }

    private static Bounds MeasureBakedWorldBounds(GameObject actor)
    {
        var mesh = new Mesh();
        var bounds = new Bounds();
        bool found = false;
        try {
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (!skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                // Use the explicit scale convention and measure actual vertices. Renderer.localBounds
                // is a culling estimate and can stay plausible while the visible rig is incorrectly sized.
                skin.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices) {
                    var point = skin.transform.TransformPoint(vertex);
                    Assert.IsFalse(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z));
                    Assert.IsFalse(float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            Assert.IsTrue(found, actor.name + " has no baked visible vertices.");
            return bounds;
        }
        finally { Object.Destroy(mesh); }
    }

    private static void AssertEffect(SequenceEffect effect, Transform actor)
    {
        Assert.IsNotNull(effect); Assert.IsTrue(effect.transform.IsChildOf(actor));
        Assert.IsNotNull(effect.frames); Assert.Greater(effect.frames.Length, 0); Assert.IsTrue(effect.frames.All(f => f != null));
        Assert.IsNotNull(effect.GetComponent<MeshRenderer>().sharedMaterial);
    }

    private static void AssertNoVisibleLegacySkin()
    {
        foreach (var skin in SceneComponents<SkinnedMeshRenderer>())
            if (skin.enabled && skin.gameObject.activeInHierarchy && skin.sharedMesh != null)
                StringAssert.DoesNotContain("/Selected/Rigged/", AssetDatabase.GetAssetPath(skin.sharedMesh), skin.name);
    }

    private static Dictionary<Image, Sprite> CapturePurchasedSkin()
    {
        var result = new Dictionary<Image, Sprite>();
        foreach (var binding in SceneComponents<UISkinBinding>()) {
            Assert.IsNotNull(binding.profile, binding.name); Assert.IsEmpty(binding.profile.ValidateConfiguration());
            Assert.IsEmpty(binding.ValidateApplied(), binding.name + " must save the applied skin, not just a profile reference.");
            foreach (var slot in binding.images) {
                Assert.IsNotNull(slot); Assert.IsNotNull(slot.target, binding.name);
                Assert.IsNotNull(slot.target.sprite, binding.name + "/" + slot.target.name);
                StringAssert.StartsWith("Assets/_Game/Art/Selected/StageD/UI/", AssetDatabase.GetAssetPath(slot.target.sprite));
                result[slot.target] = slot.target.sprite;
            }
        }
        return result;
    }

    private static void AssertSkinUnchanged(Dictionary<Image, Sprite> before)
    {
        foreach (var entry in before) { Assert.IsNotNull(entry.Key); Assert.AreSame(entry.Value, entry.Key.sprite, entry.Key.name + " lost its authored skin during a business refresh."); }
    }

    private static void AssertLiveText(Text text)
    {
        Assert.IsNotNull(text.font, text.name); Assert.IsNotNull(text.font.material, text.name); Assert.IsNotNull(text.font.material.mainTexture, text.name);
    }

    private static bool HasCallback(Button button, Object target, string method)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == target && button.onClick.GetPersistentMethodName(i) == method) return true;
        return false;
    }

    private static T[] SceneComponents<T>() where T : Component
    {
        return SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }

    private static IEnumerator Load(string scene)
    {
        yield return SceneManager.LoadSceneAsync(scene); yield return null;
    }
}
#endif
