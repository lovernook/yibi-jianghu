using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using Yibi.Battle;
using Yibi.World;

namespace Yibi.Editor
{
    public static class NetworkPerformanceSetup
    {
        const string UI="Assets/_Game/Art/Selected/UI/";
        [MenuItem("一笔江湖/联机/完善房间与恢复界面")]
        public static void SaveNetworkUI()
        {
            PolishEnvironmentSetup.Guard();var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/Lobby.unity");var lobby=UnityEngine.Object.FindObjectOfType<LobbyPresenter>();
            Unpack(lobby.gameObject);lobby.createButton=lobby.transform.Find("创建房间").GetComponent<Button>();lobby.joinButton=lobby.transform.Find("加入房间").GetComponent<Button>();lobby.readyButton=lobby.transform.Find("准备").GetComponent<Button>();lobby.resumeButton=lobby.transform.Find("恢复").GetComponent<Button>();
            lobby.address.characterLimit=253;lobby.transform.Find("说明").GetComponent<Text>().text="同机填写 127.0.0.1，局域网填写服务电脑的地址。可使用 IP:端口。\n创建或加入六位房号，双方选好构筑后准备；断线自动恢复，同意后可再战。";
            var summary=lobby.transform.Find("房间概况");lobby.roomSummary=summary==null?ArenaSetup.Label("房间概况",lobby.transform,"创建房间后，将六位房号告诉另一位玩家。",new Vector2(0,-40),new Vector2(1040,35),20):summary.GetComponent<Text>();lobby.status.rectTransform.anchoredPosition=new Vector2(0,-82);lobby.status.rectTransform.sizeDelta=new Vector2(1060,40);lobby.status.fontSize=19;
            Font(lobby.gameObject);PrefabUtility.SaveAsPrefabAssetAndConnect(lobby.gameObject,"Assets/_Game/Prefabs/RoomHUD.prefab",InteractionMode.AutomatedAction);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            foreach(string name in new[]{"Arena_Stone","Arena_Bamboo"}){
                scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+name+".unity");var battle=UnityEngine.Object.FindObjectOfType<BattlePresenter>();Unpack(battle.gameObject);
                var controller=battle.GetComponent<NetworkBattlePanel>();if(controller==null){controller=battle.gameObject.AddComponent<NetworkBattlePanel>();controller.battle=battle;
                    var panel=ArenaSetup.Panel("联机状态与恢复",battle.transform,new Vector2(0,153),new Vector2(1030,38),new Color(.04f,.11f,.14f,.96f));controller.panel=panel.gameObject;
                    controller.status=ArenaSetup.Label("网络状态",panel.transform,"等待权威状态",new Vector2(-220,0),new Vector2(560,36),15);
                    controller.rematch=Button("再战",panel.transform,"再战一局",new Vector2(160,0),new Vector2(140,44));UnityEventTools.AddPersistentListener(controller.rematch.onClick,controller.Rematch);
                    controller.room=Button("返回房间",panel.transform,"回房间调整",new Vector2(310,0),new Vector2(140,44));UnityEventTools.AddPersistentListener(controller.room.onClick,controller.Room);
                    controller.recover=Button("立即恢复",panel.transform,"重试恢复",new Vector2(450,0),new Vector2(125,44));UnityEventTools.AddPersistentListener(controller.recover.onClick,controller.Recover);
                }
                foreach(var button in new[]{controller.rematch,controller.room,controller.recover}){var rect=button.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(rect.sizeDelta.x,32);button.GetComponentInChildren<Text>().fontSize=15;}
                var networkRect=controller.panel.GetComponent<RectTransform>();networkRect.anchoredPosition=new Vector2(0,112);networkRect.sizeDelta=new Vector2(1210,40);
                controller.status.rectTransform.anchoredPosition=new Vector2(-280,0);controller.status.rectTransform.sizeDelta=new Vector2(610,36);
                controller.rematch.GetComponent<RectTransform>().anchoredPosition=new Vector2(110,0);controller.room.GetComponent<RectTransform>().anchoredPosition=new Vector2(280,0);controller.recover.GetComponent<RectTransform>().anchoredPosition=new Vector2(465,0);
                battle.turnLabel.rectTransform.anchoredPosition=new Vector2(140,165);battle.turnLabel.rectTransform.sizeDelta=new Vector2(920,35);battle.turnLabel.color=new Color(1,.94f,.78f);
                battle.feedback.intention.rectTransform.anchoredPosition=new Vector2(0,64);battle.feedback.intention.rectTransform.sizeDelta=new Vector2(1000,40);
                var legacyRecover=battle.transform.Find("恢复连接");if(legacyRecover!=null)UnityEngine.Object.DestroyImmediate(legacyRecover.gameObject);
                Font(battle.gameObject);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
        }
        static void Unpack(GameObject g){if(PrefabUtility.IsPartOfPrefabInstance(g))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(g),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);}
        static void Font(GameObject g){var f=UnityEngine.Font.CreateDynamicFontFromOSFont("Microsoft YaHei",24);foreach(var text in g.GetComponentsInChildren<Text>(true))text.font=f;}
        static Button Button(string name,Transform p,string text,Vector2 xy,Vector2 size){var b=ArenaSetup.Button(name,p,text,xy,size);b.image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(UI+"青玉按钮.asset");b.image.type=Image.Type.Sliced;b.GetComponentInChildren<Text>().fontSize=17;return b;}
        [MenuItem("一笔江湖/性能/整理已用 UI 切片")]
        public static void PackUI()
        {
            PolishEnvironmentSetup.Guard();const string path=UI+"CompactRuntimeUI.png";
            var sprites=AssetDatabase.FindAssets("t:Sprite",new[]{UI}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".asset")).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToArray();if(sprites.All(s=>AssetDatabase.GetAssetPath(s.texture)==path))return;
            var folder=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).Parent.FullName,"Backups/Before-Network-Performance/UISlices");System.IO.Directory.CreateDirectory(folder);
            var crops=new System.Collections.Generic.List<Texture2D>();var packed=new Texture2D(2,2,TextureFormat.RGBA32,false);var oldTarget=RenderTexture.active;
            try{
                foreach(var sprite in sprites){var source=AssetDatabase.GetAssetPath(sprite);var backup=System.IO.Path.Combine(folder,System.IO.Path.GetFileName(source));if(!System.IO.File.Exists(backup))System.IO.File.Copy(source,backup);
                    var texture=sprite.texture;var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                    try{Graphics.Blit(texture,rt);RenderTexture.active=rt;var crop=new Texture2D((int)sprite.rect.width,(int)sprite.rect.height,TextureFormat.RGBA32,false);crop.ReadPixels(sprite.rect,0,0);crop.Apply();crops.Add(crop);}finally{RenderTexture.active=oldTarget;RenderTexture.ReleaseTemporary(rt);}
                }
                var rects=packed.PackTextures(crops.ToArray(),4,2048,false);
                for(int i=0;i<sprites.Length;i++)if(Mathf.RoundToInt(rects[i].width*packed.width)!=crops[i].width||Mathf.RoundToInt(rects[i].height*packed.height)!=crops[i].height)throw new InvalidOperationException("Packing would reduce sprite resolution");
                System.IO.File.WriteAllBytes(path,packed.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                for(int i=0;i<sprites.Length;i++){var rect=rects[i];var old=sprites[i];var replacement=Sprite.Create(atlas,new Rect(Mathf.Round(rect.x*atlas.width),Mathf.Round(rect.y*atlas.height),Mathf.Round(rect.width*atlas.width),Mathf.Round(rect.height*atlas.height)),new Vector2(old.pivot.x/old.rect.width,old.pivot.y/old.rect.height),old.pixelsPerUnit,0,SpriteMeshType.FullRect,old.border);replacement.name=old.name;EditorUtility.CopySerialized(replacement,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(replacement);}
                AssetDatabase.SaveAssets();
            }finally{RenderTexture.active=oldTarget;foreach(var crop in crops)UnityEngine.Object.DestroyImmediate(crop);UnityEngine.Object.DestroyImmediate(packed);}
        }
        public static void SaveMemoryPolicies()
        {
            PolishEnvironmentSetup.Guard();foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled)){
                var scene=EditorSceneManager.OpenScene(entry.path);if(UnityEngine.Object.FindObjectOfType<Yibi.Core.SceneMemoryPolicy>()==null)new GameObject("场景资源优化",typeof(Yibi.Core.SceneMemoryPolicy));
                var root=GameObject.Find("晴空归云_场景细化");if(root!=null)foreach(var child in root.transform.Cast<Transform>().Where(t=>t.name=="木围栏"&&!t.gameObject.activeSelf).ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/MainMenu.unity");
        }
    }
}
