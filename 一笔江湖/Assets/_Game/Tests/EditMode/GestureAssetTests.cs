using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Yibi.Rules;
using Yibi.UI;

public class GestureAssetTests
{
    [Test]
    public void OverlappingNodesCannotOverwriteSavedRoute()
    {
        string path="Assets/_Game/Data/Gestures/Test_"+System.Guid.NewGuid().ToString("N")+".asset";
        var asset=ScriptableObject.CreateInstance<GestureTemplateSO>();asset.stableId="test";asset.nodes=new[]{new Vector2(.2f,.2f),new Vector2(.7f,.35f),new Vector2(.3f,.6f),new Vector2(.7f,.85f)};
        try
        {
            AssetDatabase.CreateAsset(asset,path);AssetDatabase.SaveAssets();
            string savedContent=System.IO.File.ReadAllText(path);
            asset.nodes[1]=asset.nodes[0];EditorUtility.SetDirty(asset);
            LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("拒绝保存非法穴位路线"));
            AssetDatabase.SaveAssets();
            // A dirty in-memory object is intentionally retained for correction; verify the actual file.
            Assert.That(System.IO.File.ReadAllText(path),Is.EqualTo(savedContent));
        }
        finally {AssetDatabase.DeleteAsset(path);}
    }

    [Test]
    public void MaxPointScoringMicrobenchmark()
    {
        var route=StarterRoutes.Create()[0];var list=new System.Collections.Generic.List<QuantizedPoint>(FixedGestureSamples.Dense(route,170));list.Add(list[list.Count-1]);
        Assert.That(list.Count,Is.EqualTo(512));
        for(int i=0;i<50;i++)GestureScorer.Score(route,list);
        var samples=new double[300];var timer=new System.Diagnostics.Stopwatch();
        for(int i=0;i<samples.Length;i++){timer.Restart();var result=GestureScorer.Score(route,list);timer.Stop();Assert.That(result.Valid,Is.True);samples[i]=timer.Elapsed.TotalMilliseconds;}
        System.Array.Sort(samples);
        TestContext.WriteLine("Editor microbenchmark only: 512 points, warmup=50, n=300, P95_ms="+samples[284].ToString("F4"));
    }
}
