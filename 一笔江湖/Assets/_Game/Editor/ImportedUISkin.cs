using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using Yibi.Battle;

namespace Yibi.Editor
{
    public static class ImportedUISkin
    {
        private const string AtlasPath="Assets/_Game/Art/界面ui/游戏界面设计元素 (15).jpg";
        private const string Folder="Assets/_Game/Art/Selected/UI";
        // Coordinates refer to the supplied 6200x4096 sheet. Sprite metadata keeps the original bitmap untouched.
        private static Sprite Slice(Texture2D atlas,string name,int x,int top,int width,int height,Vector4 border)
        {
            string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(existing!=null)return existing;
            var sprite=Sprite.Create(atlas,new Rect(x,atlas.height-top-height,width,height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);sprite.name=name;AssetDatabase.CreateAsset(sprite,path);return sprite;
        }
        [MenuItem("一笔江湖/素材/应用导入武侠 UI 图集")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save and exit Play first");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var importer=(TextureImporter)AssetImporter.GetAtPath(AtlasPath);importer.maxTextureSize=8192;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);if(atlas.width!=6200||atlas.height!=4096)throw new System.InvalidOperationException("Unexpected atlas dimensions "+atlas.width+"x"+atlas.height);
            var panel=Slice(atlas,"武侠蓝纹面板",3565,1625,555,380,new Vector4(28,28,28,28));
            var buttonAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/界面ui/游戏界面设计元素 (3).jpg");
            var button=Slice(buttonAtlas,"青玉按钮",864,610,123,23,new Vector4(6,5,6,5));
            var scroll=Slice(atlas,"水墨卷轴",4300,570,600,320,new Vector4(38,30,38,30));
            string smallPath="Assets/_Game/Art/界面ui/游戏界面设计元素 (5).jpg";var small=AssetDatabase.LoadAssetAtPath<Texture2D>(smallPath);
            var icons=new Sprite[6];for(int i=0;i<5;i++)icons[i]=Slice(small,"秘籍图标_"+i,16+i*32,912,20,20,Vector4.zero);icons[5]=Slice(small,"秘籍图标_5",16,944,24,24,Vector4.zero);
            foreach(var prefabName in new[]{"ValleyHUD","BattleHUD","RoomHUD","GestureLabUI"}){
                string path="Assets/_Game/Prefabs/"+prefabName+".prefab";if(!File.Exists(path))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                foreach(var img in root.GetComponentsInChildren<Image>(true)){
                    if(img.GetComponentInParent<Yibi.UI.GestureBoard>()!=null||img.name.Contains("分项")||img.name.Contains("高亮")||img.name.Contains("网格")||img.name=="TechniqueIcon")continue;
                    img.sprite=img.GetComponent<Button>()!=null?button:panel;img.type=Image.Type.Sliced;img.color=Color.white;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
            }
            foreach(var sceneName in new[]{"Valley","Arena_Stone","Arena_Bamboo","GestureLab","Lobby"}){
                var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/"+sceneName+".unity");
                foreach(var canvas in Object.FindObjectsOfType<Canvas>()){
                    foreach(var img in canvas.GetComponentsInChildren<Image>(true)){
                        if(img.GetComponentInParent<Yibi.UI.GestureBoard>()!=null)continue;
                        if(img.name.Contains("分项")||img.name.Contains("高亮")||img.name.Contains("网格")||img.name=="TechniqueIcon")continue;
                        Undo.RecordObject(img,"替换资源 UI");img.sprite=img.GetComponent<Button>()!=null?button:panel;img.type=Image.Type.Sliced;img.color=Color.white;PrefabUtility.RecordPrefabInstancePropertyModifications(img);EditorUtility.SetDirty(img);
                    }
                    foreach(var b in canvas.GetComponentsInChildren<Button>(true)){
                        Undo.RecordObject(b,"替换按钮状态");var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1,.91f,.64f);c.pressedColor=new Color(.66f,.88f,1);c.disabledColor=new Color(.38f,.43f,.5f);b.colors=c;PrefabUtility.RecordPrefabInstancePropertyModifications(b);EditorUtility.SetDirty(b);
                    }
                }
                var battle=Object.FindObjectOfType<BattlePresenter>();if(battle!=null){
                    battle.techniqueIcons=icons;battle.skillIcons=new Image[3];
                    for(int i=0;i<3;i++){var b=battle.skillButtons[i];var found=b.transform.Find("TechniqueIcon");var icon=found!=null?found.GetComponent<Image>():ArenaSetup.Rect("TechniqueIcon",b.transform,new Vector2(-66,0),new Vector2(38,38)).gameObject.AddComponent<Image>();icon.sprite=icons[i];icon.raycastTarget=false;icon.preserveAspect=true;battle.skillIcons[i]=icon;var label=b.GetComponentInChildren<Text>();label.rectTransform.anchoredPosition=new Vector2(22,0);label.rectTransform.sizeDelta=new Vector2(140,75);label.fontSize=17;}
                }
                if(battle!=null){PrefabUtility.RecordPrefabInstancePropertyModifications(battle);EditorUtility.SetDirty(battle);}
                foreach(var text in Object.FindObjectsOfType<Text>(true)){PrefabUtility.RecordPrefabInstancePropertyModifications(text.rectTransform);PrefabUtility.RecordPrefabInstancePropertyModifications(text);}
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/_Game/Scenes/Valley.unity");
        }
    }
}
