using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Yibi.Battle;
using Yibi.Presentation;

public sealed class StageCArtTests
{
    const string Selected = "Assets/_Game/Art/Selected/StageC/";
    const string Profile = "Assets/_Game/Content/Presentation/GuiyunCharacterMotion.asset";

    [Test]
    public void DuplicateActionAndInvalidTimingAreRejectedWithoutMutatingSavedProfile()
    {
        var profile = ScriptableObject.CreateInstance<CharacterMotionProfile>();
        try {
            Assert.IsEmpty(profile.ValidateConfiguration());
            profile.actions[1].id = profile.actions[0].id;
            profile.actions[0].durationSeconds = float.NaN;
            profile.actions[2].durationSeconds = 0;
            profile.actions[3].transitionSeconds = -1;
            var errors = profile.ValidateConfiguration();
            Assert.That(errors.Any(e => e.Contains("重复")));
            Assert.AreEqual(2, errors.Count(e => e.Contains("时长")));
            Assert.That(errors.Any(e => e.Contains("过渡")));
        }
        finally { Object.DestroyImmediate(profile); }
    }

    [Test]
    public void SavedSelectionAndActorsReferenceEditableAssetsWithoutSourceBulkDependency()
    {
        var profile = AssetDatabase.LoadAssetAtPath<CharacterMotionProfile>(Profile);
        Assert.IsNotNull(profile); Assert.IsEmpty(profile.ValidateConfiguration());
        foreach (string name in new[] {"侠客_劲装", "行者_青衫", "村中长者", "侠女_素衣", "行脚客", "布衣药师", "白发隐士", "谷中书生"}) {
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Selected/Rigged/" + name + ".prefab");
            Assert.IsNotNull(actor, name);
            foreach (var motion in actor.GetComponentsInChildren<CharacterMotion>(true)) Assert.IsNotNull(motion.profile, name);
        }
        var effect = AssetDatabase.LoadAssetAtPath<GameObject>(Selected + "Effects/AttackSequence.prefab");
        Assert.IsNotNull(effect);
        var sequence = effect.GetComponent<SequenceEffect>();
        Assert.IsNotNull(sequence); Assert.AreEqual(8, sequence.frames.Length);
        Assert.AreEqual(16, sequence.framesPerSecond);
        Assert.IsFalse(effect.GetComponent<MeshRenderer>().enabled);
        Assert.AreEqual("Yibi/SequenceAdditive", effect.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
        foreach (var frame in sequence.frames) {
            Assert.IsNotNull(frame);
            StringAssert.StartsWith(Selected + "Effects/", AssetDatabase.GetAssetPath(frame));
        }
        foreach (string name in new[] {"Shield", "Burn"}) {
            string path = Selected + "UI/" + name + ".png";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsFalse(importer.mipmapEnabled); Assert.IsFalse(importer.isReadable);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
        }
        var dependencies = AssetDatabase.GetDependencies(Selected + "Effects/AttackSequence.prefab", true);
        Assert.IsFalse(dependencies.Any(p => p.StartsWith("Assets/_Game/资源/")), "Saved effect must depend on selected frames only.");
    }
}
