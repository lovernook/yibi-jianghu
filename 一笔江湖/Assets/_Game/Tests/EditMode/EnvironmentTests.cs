using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class EnvironmentTests
{
    [Test]
    public void SavedBootstrapAndPipelineExist()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Game/Scenes/Bootstrap.unity"), Is.Not.Null);
        Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/EnvironmentProbe.prefab")
            .GetComponent<Yibi.EnvironmentProbe>(), Is.Not.Null);
    }
}
