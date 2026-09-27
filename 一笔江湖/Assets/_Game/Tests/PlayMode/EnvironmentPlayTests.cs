using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EnvironmentPlayTests
{
    [UnityTest]
    public IEnumerator SavedSceneStartsWithCameraAndProbe()
    {
        LogAssert.Expect(LogType.Log, "YIBI_M0_STARTED scene=Bootstrap");
        yield return SceneManager.LoadSceneAsync("Bootstrap");
        yield return null;
        Assert.That(Camera.main, Is.Not.Null);
        var probe = Object.FindObjectOfType<Yibi.EnvironmentProbe>();
        Assert.That(probe, Is.Not.Null);
        Assert.That(probe.Started, Is.True);
        LogAssert.NoUnexpectedReceived();
    }
}
