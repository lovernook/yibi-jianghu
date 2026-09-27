using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Yibi.UI;
using Yibi.Rules;
namespace Yibi.Editor
{
    public static class ContentExporter
    {
        [MenuItem("一笔江湖/配置/校验并导出客户端与服务端内容")]
        public static void Export()
        {
            var catalog=ContentCatalog.Default();
            for(int i=0;i<6;i++){
                string id=BattleContent.Ids[i];var so=AssetDatabase.LoadAssetAtPath<GestureTemplateSO>("Assets/_Game/Data/Gestures/"+id+".asset");
                if(so==null||so.stableId!=id||so.ValidationError()!=null)throw new InvalidDataException("路线无效："+id);
                var nodes=new QuantizedPoint[so.nodes.Length];for(int n=0;n<nodes.Length;n++)nodes[n]=QuantizedPoint.From(new Point2(so.nodes[n].x,so.nodes[n].y));
                catalog.routes[i]=new RouteContent{id=id,cost=BattleContent.Costs[i],radius=(int)Math.Round(so.radius*10000),nodes=nodes};
            }
            catalog.Validate();string json=JsonUtility.ToJson(catalog,true);Directory.CreateDirectory("Assets/_Game/Resources");Directory.CreateDirectory("../Server/Content");
            File.WriteAllText("Assets/_Game/Resources/ContentCatalog.json",json);File.WriteAllText("../Server/Content/ContentCatalog.json",json);File.WriteAllText("../Docs/Evidence/content-sha256.txt",catalog.Hash());AssetDatabase.Refresh();Debug.Log("内容导出成功 "+catalog.Hash());
        }
    }
}
