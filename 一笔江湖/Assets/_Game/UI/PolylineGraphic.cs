using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PolylineGraphic : MaskableGraphic
    {
        [SerializeField] private List<Vector2> points=new List<Vector2>(512);
        public float thickness=3;
        public void SetPoints(IReadOnlyList<QuantizedPoint> input)
        {
            points.Clear();
            for(int i=0;i<input.Count;i++)points.Add(new Vector2(input[i].x/10000f,input[i].y/10000f));
            SetVerticesDirty();
        }
        public void SetTemplate(Vector2[] input)
        {points.Clear();if(input!=null)points.AddRange(input);SetVerticesDirty();}
        public void Clear(){points.Clear();SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            for(int i=1;i<points.Count;i++)
            {
                var a=new Vector2(r.xMin+points[i-1].x*r.width,r.yMin+points[i-1].y*r.height);
                var b=new Vector2(r.xMin+points[i].x*r.width,r.yMin+points[i].y*r.height);
                var delta=b-a;if(delta.sqrMagnitude<.001f)continue;
                var normal=new Vector2(-delta.y,delta.x).normalized*thickness*.5f;
                int index=vh.currentVertCount;
                vh.AddVert(a-normal,color,Vector2.zero);vh.AddVert(a+normal,color,Vector2.zero);
                vh.AddVert(b+normal,color,Vector2.zero);vh.AddVert(b-normal,color,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
