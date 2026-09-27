Shader "Yibi/Guiyun Sky"
{
    Properties
    {
        _Zenith ("天顶", Color) = (.21,.43,.64,1)
        _Horizon ("天际薄雾", Color) = (.73,.83,.83,1)
        _CloudColor ("云光", Color) = (.96,.94,.83,1)
        _CloudCover ("云量", Range(0,1)) = .48
        _SunDirection ("太阳方向", Vector) = (.3,.45,.8,0)
        _Wind ("云速", Range(0,.02)) = .002
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float3 ray:TEXCOORD0; };
            float4 _Zenith,_Horizon,_CloudColor,_SunDirection;
            float _CloudCover,_Wind;
            v2f vert(float4 vertex:POSITION) { v2f o; o.pos=UnityObjectToClipPos(vertex); o.ray=vertex.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            float fbm(float2 p) { return noise(p)*.55+noise(p*2.03+17)*.28+noise(p*4.07+41)*.12+noise(p*8.13)*.05; }
            fixed4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.ray); float h=saturate(d.y);
                float3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.55));
                float2 uv=d.xz/max(.16,d.y+.22)*2.2+float2(_Time.y*_Wind,0);
                float cloud=smoothstep(.67-_CloudCover*.34,.84-_CloudCover*.22,fbm(uv))*smoothstep(0,.14,d.y);
                col=lerp(col,_CloudColor.rgb,cloud*.88);
                float sun=saturate(dot(d,normalize(_SunDirection.xyz)));
                col+=float3(1,.72,.36)*(pow(sun,500)*.65+pow(sun,16)*.07)*(1-cloud*.8);
                return float4(col,1);
            }
            ENDCG
        }
    }
}
