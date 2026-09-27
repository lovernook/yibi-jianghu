using UnityEngine;
using Yibi.Rules;

namespace Yibi.UI
{
    [CreateAssetMenu(menuName="一笔江湖/穴位路线")]
    public sealed class GestureTemplateSO : ScriptableObject
    {
        public string stableId;
        public string displayName;
        [TextArea] public string description;
        [Range(.01f,.15f)] public float radius=.045f;
        public Vector2[] nodes;
        public GestureTemplate ToRules()
        {
            if(nodes==null) return new GestureTemplate(stableId,null,radius);
            var points=new Point2[nodes.Length];
            for(int i=0;i<points.Length;i++)points[i]=new Point2(nodes[i].x,nodes[i].y);
            return new GestureTemplate(stableId,points,radius);
        }
        public string ValidationError()=>PolylineGeometry.Validate(ToRules());
    }
}
