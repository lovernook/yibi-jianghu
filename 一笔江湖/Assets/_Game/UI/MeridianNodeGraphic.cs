using UnityEngine;
using UnityEngine.UI;

namespace Yibi.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MeridianNodeGraphic : MaskableGraphic
    {
        public float ringWidth=2;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            var center=rectTransform.rect.center;
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int n=vh.currentVertCount;
                vh.AddVert(center+u*radius,color,Vector2.zero);vh.AddVert(center+v*radius,color,Vector2.zero);
                vh.AddVert(center+v*(radius-ringWidth),color,Vector2.zero);vh.AddVert(center+u*(radius-ringWidth),color,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
