using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Yibi.Networking;
public class NetworkClientContractTests
{
    [TestCase("127.0.0.1",true,"127.0.0.1",7777)]
    [TestCase(" localhost:7788 ",true,"localhost",7788)]
    [TestCase("",false,null,0)]
    [TestCase("http://localhost",false,null,0)]
    [TestCase("localhost:0",false,null,0)]
    [TestCase("localhost:65536",false,null,0)]
    [TestCase(":7777",false,null,0)]
    [TestCase("two hosts",false,null,0)]
    public void AddressHasExplicitValidation(string address,bool valid,string expected,int expectedPort)
    {string host,error;int port;Assert.AreEqual(valid,NetSession.TryAddress(address,out host,out port,out error));if(valid){Assert.AreEqual(expected,host);Assert.AreEqual(expectedPort,port);}else Assert.IsNotEmpty(error);}
    [Test] public void UsedUISlicesShareCompactTextureAndPreserveBorders()
    {
        var guids=AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/_Game/Art/Selected/UI"}).Where(g=>AssetDatabase.GUIDToAssetPath(g).EndsWith(".asset")).ToArray();Assert.AreEqual(10,guids.Length,"Six icons, two buttons, scroll and panel");
        foreach(var guid in guids){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));Assert.IsNotNull(sprite);Assert.AreEqual("Assets/_Game/Art/Selected/UI/CompactRuntimeUI.png",AssetDatabase.GetAssetPath(sprite.texture));Assert.AreEqual(1024,sprite.texture.width);Assert.LessOrEqual(sprite.rect.xMax,1024);Assert.LessOrEqual(sprite.rect.yMax,1024);Assert.Greater(sprite.rect.width,0);}
        Assert.Greater(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Selected/UI/武侠蓝纹面板.asset").border.sqrMagnitude,0);
    }
}
